using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace MssqlExporter.E2E.Tests;

[TestClass]
public sealed partial class MetricsE2ETests
{
    private static readonly string Image = Environment.GetEnvironmentVariable("MSSQL_IMAGE") ?? "mcr.microsoft.com/mssql/server:2022-latest";
    private const string Password = "Str0ng_Passw0rd!";

    /// <summary>Least-privilege login provisioned from <c>Sql/exporter-permissions.sql</c>.</summary>
    private const string ExporterLogin = "exporter";

    /// <summary>Same grants, but without EXECUTE on msdb.dbo.agent_datetime.</summary>
    private const string NoExecLogin = "exporter_noexec";

    private const string ExporterPassword = "Str0ng_Exp0rter!";
    private const string Job = "exporter_e2e_job";

    private static readonly string[] MetaNames =
        ["mssql_scrape_duration_seconds", "mssql_collector_duration_seconds", "mssql_collector_success"];

    private static MsSqlContainer? container;
    private static SqlConnection? admin;

    private static IEnumerable<string> GaugeNames(Collectors.Collector collector) => collector.Gauges.Select(g => g.Value.Name ?? g.Key);

    /// <summary>Every metric family the exporter declares (collectors + scrape-quality gauges).</summary>
    private static readonly HashSet<string> DeclaredNames = [.. Collectors.Collectors.All.SelectMany(GaugeNames), .. MetaNames];

    /// <summary>Families that may legitimately be absent on a vanilla instance (Always On off, no Agent jobs, ...).</summary>
    private static readonly HashSet<string> OptionalNames = [.. Collectors.Collectors.All.Where(c => c.Optional).SelectMany(GaugeNames)];

    [GeneratedRegex(@"collector=""([^""]+)""")]
    private static partial Regex CollectorLabel();

    [ClassInitialize]
    public static async Task Init(TestContext _)
    {
        if (!ContainerRuntime.IsAvailable())
        {
            Assert.Inconclusive("No container runtime (Docker or Podman) is reachable.");
        }

        // Program reads these eagerly; each test then supplies its own options.
        Environment.SetEnvironmentVariable("MSSQL_SERVER", "unused");
        Environment.SetEnvironmentVariable("MSSQL_USER", "unused");
        Environment.SetEnvironmentVariable("MSSQL_PASSWORD", "unused");

        container = new MsSqlBuilder(Image)
            .WithPassword(Password)
            // Equivalent to `mssql-conf set sqlagent.enabled true`. Without it the Agent collectors have
            // nothing to report and the msdb grants the README documents are never exercised.
            .WithEnvironment("MSSQL_AGENT_ENABLED", "true")
            .Build();
        await container.StartAsync();

        // Provision the documented least-privilege logins and an Agent job as sa.
        admin = new SqlConnection(container.GetConnectionString());
        await admin.OpenAsync();

        foreach (var login in new[] { ExporterLogin, NoExecLogin })
        {
            await Provisioner.RunScriptAsync(
                admin,
                "exporter-permissions.sql",
                new Dictionary<string, string> { ["__LOGIN__"] = login, ["__PASSWORD__"] = ExporterPassword });
        }

        // The negative case: everything the README grants except this one.
        await Provisioner.ExecuteAsync(admin, "USE msdb");
        await Provisioner.ExecuteAsync(admin, $"REVOKE EXECUTE ON [dbo].[agent_datetime] TO [{NoExecLogin}]");

        await Provisioner.WaitForAgentAsync(admin);
        await Provisioner.RetryAsync(
            "the msdb job procedures to accept writes",
            () => Provisioner.RunScriptAsync(admin, "agent-job.sql", new Dictionary<string, string> { ["__JOB__"] = Job }));
        await Provisioner.RunJobOnceAsync(admin, Job);
    }

    [ClassCleanup]
    public static async Task Cleanup()
    {
        if (admin is not null)
        {
            await admin.DisposeAsync();
        }
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    private static string Host => container!.Hostname;

    private static int Port => container!.GetMappedPublicPort(1433);

    private static WebApplicationFactory<Program> CreateApp(string user, string password) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddSingleton(new ExporterOptions
            {
                Server = Host,
                Port = Port,
                User = user,
                Password = password,
                Encrypt = false,
                TrustServerCertificate = true,
                CollectDefaultMetrics = false,
            })));

    private static IEnumerable<string> SampleLines(string body) =>
        body.ReplaceLineEndings("\n").Split('\n').Where(line => line.Length > 0 && !line.StartsWith('#'));

    /// <summary>Metric family names that actually produced a sample in the scrape.</summary>
    private static HashSet<string> ScrapedNames(string body) =>
        [.. SampleLines(body).Select(line => line.Split([' ', '{'], 2)[0]).Where(name => name.StartsWith("mssql_", StringComparison.Ordinal))];

    /// <summary>Samples as <c>{ "mssql_up": 1, "mssql_database_state{database=\"master\"}": 0, ... }</c>.</summary>
    private static Dictionary<string, double> AsMap(string body) =>
        SampleLines(body).ToDictionary(line => line[..line.LastIndexOf(' ')], line => ParseValue(line));

    private static double ParseValue(string line) =>
        double.Parse(line[(line.LastIndexOf(' ') + 1)..], System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Per-collector outcome keyed by collector name: <c>{ mssql_agent_jobs: 1, ... }</c>.</summary>
    private static Dictionary<string, double> CollectorSuccess(string body) =>
        SampleLines(body)
            .Where(line => line.StartsWith("mssql_collector_success{", StringComparison.Ordinal))
            .ToDictionary(
                line => CollectorLabel().Match(line).Groups[1].Value,
                ParseValue);

    private static List<string> MissingRequired(string body)
    {
        var scraped = ScrapedNames(body);
        return [.. DeclaredNames.Where(name => !OptionalNames.Contains(name) && !scraped.Contains(name))];
    }

    /// <summary>Scrape until every required family is present (some DMVs lag just after boot).</summary>
    private static async Task<string> ScrapeWhenReadyAsync(HttpClient client)
    {
        var text = "";
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var response = await client.GetAsync("/metrics");
            text = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode && MissingRequired(text).Count == 0)
            {
                return text;
            }
            await Task.Delay(2_000);
        }
        return text;
    }

    [TestMethod]
    public async Task ScrapesARealInstanceAndExposesEveryRequiredMetricFamily()
    {
        using var factory = CreateApp("sa", Password);
        using var client = factory.CreateClient();

        var text = await ScrapeWhenReadyAsync(client);
        var metrics = AsMap(text);

        Assert.AreEqual(1d, metrics["mssql_up"]);
        Assert.IsGreaterThanOrEqualTo(15d, metrics["mssql_product_version"]);
        Assert.IsGreaterThan(0d, metrics["mssql_instance_local_time"]);
        Assert.IsGreaterThan(0d, metrics["mssql_total_physical_memory_kb"]);
        Assert.IsGreaterThan(0d, metrics["mssql_scrape_duration_seconds"]);
        Assert.AreEqual(0d, metrics["mssql_database_state{database=\"master\"}"]);

        // every required family present ...
        CollectionAssert.AreEqual(Array.Empty<string>(), MissingRequired(text));
        // ... and nothing outside what the exporter declares
        CollectionAssert.AreEqual(Array.Empty<string>(), ScrapedNames(text).Where(name => !DeclaredNames.Contains(name)).ToArray());
    }

    [TestMethod]
    public async Task ServesAnIsolatedRegistryPerProbeTarget()
    {
        using var factory = CreateApp("sa", Password);
        using var client = factory.CreateClient();

        var text = await client.GetStringAsync($"/probe?target={Host}:{Port}");

        Assert.IsTrue(text.ReplaceLineEndings("\n").Split('\n').Contains("mssql_up 1"));
        // probe output must not carry the default-registry process metrics
        Assert.IsFalse(Regex.IsMatch(text, "^process_", RegexOptions.Multiline));
    }

    [TestMethod]
    public async Task ReportsTheSqlAgentJobTheFixtureCreated()
    {
        using var factory = CreateApp("sa", Password);
        using var client = factory.CreateClient();

        var metrics = AsMap(await ScrapeWhenReadyAsync(client));

        Assert.AreEqual(1d, metrics["mssql_agent_up"]);
        Assert.AreEqual(1d, metrics[$"mssql_agent_job_enabled{{job=\"{Job}\"}}"]);
        Assert.AreEqual(1d, metrics[$"mssql_agent_job_last_run_success{{job=\"{Job}\"}}"]);
        // Non-zero only if msdb.dbo.agent_datetime() actually executed.
        Assert.IsGreaterThan(0d, metrics[$"mssql_agent_job_last_run_timestamp{{job=\"{Job}\"}}"]);
    }

    [TestMethod]
    public async Task CollectsEverythingWithOnlyThePermissionsTheReadmeDocuments()
    {
        using var saApp = CreateApp("sa", Password);
        using var exporterApp = CreateApp(ExporterLogin, ExporterPassword);
        using var saClient = saApp.CreateClient();
        using var exporterClient = exporterApp.CreateClient();

        var asSa = CollectorSuccess(await ScrapeWhenReadyAsync(saClient));
        var asExporter = CollectorSuccess(await ScrapeWhenReadyAsync(exporterClient));

        // The least-privilege login must not lose a single collector against sa.
        CollectionAssert.AreEquivalent(asSa.OrderBy(p => p.Key).ToList(), asExporter.OrderBy(p => p.Key).ToList());
        Assert.AreEqual(1d, asExporter["mssql_agent_up"]);
        Assert.AreEqual(1d, asExporter["mssql_agent_jobs"]);
    }

    [TestMethod]
    public async Task FailsTheAgentJobCollectorWithoutExecuteOnAgentDatetime()
    {
        // /probe builds a throwaway registry, so this failing scrape cannot leak into other assertions.
        using var factory = CreateApp(NoExecLogin, ExporterPassword);
        using var client = factory.CreateClient();

        var success = CollectorSuccess(await client.GetStringAsync($"/probe?target={Host}:{Port}"));

        Assert.AreEqual(0d, success["mssql_agent_jobs"]);
        // ... and only that collector: the rest of the grants are still in place.
        Assert.AreEqual(1d, success["mssql_agent_up"]);
        Assert.AreEqual(1d, success["mssql_backups"]);
        Assert.AreEqual(1d, success["mssql_suspect_pages"]);
    }
}
