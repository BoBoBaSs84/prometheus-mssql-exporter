using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using MssqlExporter.Data;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class EndpointsTests
{
    private static readonly ExporterOptions BaseOptions = new() { Server = "db", User = "sa", Password = "pw" };

    [AssemblyInitialize]
    public static void Init(TestContext _)
    {
        // Program reads these eagerly; individual tests then replace the options and the runner.
        Environment.SetEnvironmentVariable("MSSQL_SERVER", "db");
        Environment.SetEnvironmentVariable("MSSQL_USER", "sa");
        Environment.SetEnvironmentVariable("MSSQL_PASSWORD", "pw");
    }

    private static WebApplicationFactory<Program> Create(FakeSqlRunner runner, Func<ExporterOptions, ExporterOptions>? tweak = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<ISqlRunner>(runner);
            services.AddSingleton(tweak?.Invoke(BaseOptions) ?? BaseOptions);
        }));

    [TestMethod]
    public async Task RootRedirectsToMetrics()
    {
        using var factory = Create(new FakeSqlRunner());
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/metrics", response.Headers.Location?.OriginalString);
    }

    [TestMethod]
    public async Task HealthzAnswersWithoutTouchingTheDatabase()
    {
        var runner = new FakeSqlRunner();
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "\"status\":\"ok\"");
        Assert.AreEqual(0, runner.ConnectionStrings.Count);
    }

    [TestMethod]
    public async Task MetricsServesGaugesAndClosesTheConnection()
    {
        var runner = new FakeSqlRunner();
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");
        var text = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.StartsWith(response.Content.Headers.ContentType?.ToString(), "text/plain");
        StringAssert.Matches(text, new System.Text.RegularExpressions.Regex("^mssql_up 1$", System.Text.RegularExpressions.RegexOptions.Multiline));
        StringAssert.Contains(text, "mssql_scrape_duration_seconds ");
        StringAssert.Contains(text, "process_");
        Assert.AreEqual(1, runner.Disposed);
        var connection = new SqlConnectionStringBuilder(runner.ConnectionStrings.Single());
        Assert.AreEqual("db,1433", connection.DataSource);
    }

    [TestMethod]
    public async Task MetricsOmitsRuntimeMetricsWhenDisabled()
    {
        using var factory = Create(new FakeSqlRunner(), o => o with { CollectDefaultMetrics = false });
        using var client = factory.CreateClient();

        var text = await client.GetStringAsync("/metrics");

        StringAssert.Contains(text, "mssql_up 1");
        Assert.DoesNotContain("process_", text);
    }

    [TestMethod]
    public async Task MetricsReportsUpZeroWithSanitizedHeaderWhenConnectionFails()
    {
        var runner = new FakeSqlRunner { ConnectFailure = _ => new InvalidOperationException("connect refused\nsecond line") };
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");
        var text = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("connect refused second line", response.Headers.GetValues("X-Error").Single());
        StringAssert.Contains(text, "mssql_up 0");
        Assert.DoesNotContain("mssql_scrape_duration_seconds", text);
        Assert.DoesNotContain("mssql_connections", text);
    }

    [TestMethod]
    public async Task MetricsKeepsScrapingWhenASingleCollectorFails()
    {
        var connectionsSql = Collectors.Collectors.All.Single(c => c.Name == "mssql_connections").Query;
        var runner = new FakeSqlRunner
        {
            Query = sql => sql == "SELECT 1" ? [[1]] : sql == connectionsSql ? throw new InvalidOperationException("permission denied") : [],
        };
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/metrics");
        var text = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(text, "mssql_up 1");
        StringAssert.Contains(text, "mssql_scrape_duration_seconds ");
        StringAssert.Contains(text, "mssql_collector_success{collector=\"mssql_up\"} 1");
        StringAssert.Contains(text, "mssql_collector_success{collector=\"mssql_connections\"} 0");
        StringAssert.Contains(text, "mssql_collector_duration_seconds{collector=\"mssql_connections\"}");
        Assert.AreEqual(1, runner.Disposed);
    }

    [TestMethod]
    public async Task MetricsPassesTheConfiguredQueryTimeout()
    {
        var runner = new FakeSqlRunner();
        using var factory = Create(runner, o => o with { QueryTimeoutMs = 1234 });
        using var client = factory.CreateClient();

        await client.GetAsync("/metrics");

        Assert.IsNotEmpty(runner.Timeouts);
        Assert.IsTrue(runner.Timeouts.All(t => t == 1234));
    }

    [TestMethod]
    public async Task MetricsDropsSeriesThatNoLongerExist()
    {
        var connectionsSql = Collectors.Collectors.All.Single(c => c.Name == "mssql_connections").Query;
        string[] databases = ["gone", "kept"];
        var runner = new FakeSqlRunner
        {
            Query = sql => sql == connectionsSql ? [.. databases.Select(d => new object?[] { d, 1 })] : [],
        };
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        StringAssert.Contains(await client.GetStringAsync("/metrics"), "database=\"gone\"");
        databases = ["kept"];
        var second = await client.GetStringAsync("/metrics");

        Assert.DoesNotContain("database=\"gone\"", second);
        StringAssert.Contains(second, "database=\"kept\"");
    }

    [TestMethod]
    public async Task ProbeRejectsMissingOrInvalidTargets()
    {
        using var factory = Create(new FakeSqlRunner());
        using var client = factory.CreateClient();

        var missing = await client.GetAsync("/probe");
        var invalid = await client.GetAsync("/probe?target=bad target!");

        Assert.AreEqual(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.AreEqual("missing or invalid ?target=host[:port]", await missing.Content.ReadAsStringAsync());
    }

    [TestMethod]
    public async Task ProbeConnectsToTheRequestedTargetWithAnIsolatedRegistry()
    {
        var runner = new FakeSqlRunner();
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/probe?target=other-host:1444");
        var text = await response.Content.ReadAsStringAsync();

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("other-host,1444", new SqlConnectionStringBuilder(runner.ConnectionStrings.Single()).DataSource);
        StringAssert.Contains(text, "mssql_up 1");
        Assert.DoesNotContain("process_", text);
    }

    [TestMethod]
    public async Task ProbeUsesTheConfiguredPortWhenTheTargetHasNone()
    {
        var runner = new FakeSqlRunner();
        using var factory = Create(runner, o => o with { Port = 1500 });
        using var client = factory.CreateClient();

        await client.GetAsync("/probe?target=other-host");

        Assert.AreEqual("other-host,1500", new SqlConnectionStringBuilder(runner.ConnectionStrings.Single()).DataSource);
    }

    [TestMethod]
    public async Task ProbeReportsUpZeroWithHeaderWhenTheTargetIsUnreachable()
    {
        var runner = new FakeSqlRunner { ConnectFailure = _ => new InvalidOperationException("nope") };
        using var factory = Create(runner);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/probe?target=down");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        Assert.AreEqual("nope", response.Headers.GetValues("X-Error").Single());
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "mssql_up 0");
    }

    [TestMethod]
    public async Task ProbeCanBeDisabled()
    {
        using var factory = Create(new FakeSqlRunner(), o => o with { ProbeEnabled = false });
        using var client = factory.CreateClient();

        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/probe?target=host")).StatusCode);
    }

    [TestMethod]
    public void SanitizeHeaderCollapsesNewlinesAndReplacesNonAscii()
    {
        Assert.AreEqual("a b", Endpoints.SanitizeHeader("a\r\n\nb"));
        Assert.AreEqual("caf? ??", Endpoints.SanitizeHeader("café 日本"));
    }
}
