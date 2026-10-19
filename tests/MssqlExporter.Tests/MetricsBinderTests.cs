using System.Text;
using MssqlExporter.Binding;
using MssqlExporter.Collectors;
using Prometheus;
using Collector = MssqlExporter.Collectors.Collector;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class MetricsBinderTests
{
    private static async Task<string> Export(CollectorRegistry registry)
    {
        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream);
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

    private static Collector Sample() => new(
        "mssql_sample",
        new Dictionary<string, GaugeSpec>
        {
            ["mssql_plain"] = new("A plain gauge"),
            ["renamed_key"] = new("Renamed gauge", Name: "mssql_renamed"),
            ["mssql_labelled"] = new("A labelled gauge", LabelNames: ["database", "state"]),
        },
        "SELECT 1",
        (_, _) => { });

    [TestMethod]
    public async Task BuildEntriesCreatesGaugesWithNameHelpAndLabels()
    {
        var registry = Metrics.NewCustomRegistry();
        var entries = MetricsBinder.BuildEntries(registry, [Sample()]);

        var gauges = entries.Single().Gauges;
        gauges.Set("mssql_plain", 5);
        gauges.Set("renamed_key", 7);
        gauges.Set("mssql_labelled", 3, ("database", "master"), ("state", "current"));

        var text = await Export(registry);
        StringAssert.Contains(text, "# HELP mssql_plain A plain gauge\n# TYPE mssql_plain gauge\nmssql_plain 5\n");
        StringAssert.Contains(text, "mssql_renamed 7\n");
        StringAssert.Contains(text, "mssql_labelled{database=\"master\",state=\"current\"} 3\n");
    }

    [TestMethod]
    public async Task NamedLabelsAreOrderedAgainstDeclaration()
    {
        var registry = Metrics.NewCustomRegistry();
        var gauges = MetricsBinder.BuildEntries(registry, [Sample()]).Single().Gauges;

        gauges.Set("mssql_labelled", 1, ("state", "current"), ("database", "master"));

        StringAssert.Contains(await Export(registry), "mssql_labelled{database=\"master\",state=\"current\"} 1\n");
    }

    [TestMethod]
    public void LabelMismatchThrows()
    {
        var gauges = MetricsBinder.BuildEntries(Metrics.NewCustomRegistry(), [Sample()]).Single().Gauges;

        Assert.ThrowsExactly<ArgumentException>(() => gauges.Set("mssql_labelled", 1, ("database", "master")));
        Assert.ThrowsExactly<ArgumentException>(() => gauges.Set("mssql_labelled", 1, ("database", "master"), ("oops", "x")));
        Assert.ThrowsExactly<ArgumentException>(() => gauges.Set("mssql_labelled", 1));
    }

    [TestMethod]
    public void UnknownGaugeKeyThrows()
    {
        var gauges = MetricsBinder.BuildEntries(Metrics.NewCustomRegistry(), [Sample()]).Single().Gauges;

        Assert.ThrowsExactly<KeyNotFoundException>(() => gauges.Set("nope", 1));
    }

    [TestMethod]
    public async Task NullValueLeavesLabelledSeriesAbsent()
    {
        var registry = Metrics.NewCustomRegistry();
        var gauges = MetricsBinder.BuildEntries(registry, [Sample()]).Single().Gauges;

        gauges.Set("mssql_labelled", null, ("database", "master"), ("state", "current"));

        Assert.DoesNotContain("mssql_labelled{", await Export(registry));
    }

    [TestMethod]
    public async Task RegistriesAreIndependent()
    {
        var first = Metrics.NewCustomRegistry();
        var second = Metrics.NewCustomRegistry();
        MetricsBinder.BuildEntries(first, [Sample()]).Single().Gauges.Set("mssql_labelled", 1, ("database", "gone"), ("state", "current"));
        MetricsBinder.BuildEntries(second, [Sample()]);

        Assert.Contains("database=\"gone\"", await Export(first));
        Assert.DoesNotContain("database=\"gone\"", await Export(second));
    }

    [TestMethod]
    public async Task BuildMetaCreatesScrapeQualityGauges()
    {
        var registry = Metrics.NewCustomRegistry();
        var meta = MetricsBinder.BuildMeta(registry);
        meta.ScrapeDuration.Set(1.5);
        meta.CollectorDuration.WithLabels("mssql_up").Set(0.25);
        meta.CollectorSuccess.WithLabels("mssql_up").Set(1);

        var text = await Export(registry);
        StringAssert.Contains(text, "# HELP mssql_scrape_duration_seconds Total time the exporter spent collecting metrics for this scrape\n");
        StringAssert.Contains(text, "mssql_scrape_duration_seconds 1.5\n");
        StringAssert.Contains(text, "# HELP mssql_collector_duration_seconds Time spent running a single collector\n");
        StringAssert.Contains(text, "mssql_collector_duration_seconds{collector=\"mssql_up\"} 0.25\n");
        StringAssert.Contains(text, "# HELP mssql_collector_success Whether a collector completed without error (1) or not (0)\n");
        StringAssert.Contains(text, "mssql_collector_success{collector=\"mssql_up\"} 1\n");
    }
}
