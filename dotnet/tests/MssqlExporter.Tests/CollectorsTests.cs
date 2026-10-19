using System.Text;
using MssqlExporter.Binding;
using MssqlExporter.Tests.Fixtures;
using Prometheus;
using Collector = MssqlExporter.Collectors.Collector;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class CollectorsTests
{
    private static IReadOnlyList<Collector> All => Collectors.Collectors.All;

    private static Collector Get(string name) => All.Single(c => c.Name == name);

    /// <summary>Runs one collector against its fixture on a fresh registry and returns the exposition text.</summary>
    private static async Task<string> Collect(string name, object?[][]? rows = null)
    {
        var registry = Metrics.NewCustomRegistry();
        var entry = MetricsBinder.BuildEntries(registry, [Get(name)]).Single();
        entry.Collector.Collect(rows ?? Rows.All[name], entry.Gauges);

        using var stream = new MemoryStream();
        await registry.CollectAndExportAsTextAsync(stream);
        return Encoding.UTF8.GetString(stream.ToArray()).ReplaceLineEndings("\n");
    }

    [TestMethod]
    public void HasAFixtureForEveryCollector() =>
        CollectionAssert.AreEquivalent(All.Select(c => c.Name).ToList(), Rows.All.Keys.ToList());

    [TestMethod]
    public void CollectorNamesAreUnique() => Assert.AreEqual(All.Count, All.Select(c => c.Name).Distinct().Count());

    [TestMethod]
    public async Task EveryCollectorCollectsItsFixtureWithoutThrowing()
    {
        foreach (var collector in All)
        {
            var text = await Collect(collector.Name);
            Assert.IsFalse(string.IsNullOrWhiteSpace(text), collector.Name);
        }
    }

    [TestMethod]
    public async Task MssqlUpRecordsTheInstanceStatus() =>
        StringAssert.Contains(await Collect("mssql_up"), "\nmssql_up 1\n".TrimStart('\n').Insert(0, ""));

    [TestMethod]
    public async Task MssqlProductVersionRecordsMajorMinor() =>
        StringAssert.Contains(await Collect("mssql_product_version"), "mssql_product_version 15\n");

    [TestMethod]
    public async Task MssqlProductVersionKeepsNodeNumberSemanticsForTwoDigitMinor() =>
        StringAssert.Contains(await Collect("mssql_product_version", [["15.10.2000.5"]]), "mssql_product_version 15.1\n");

    [TestMethod]
    public async Task MssqlProductVersionRejectsGarbage() =>
        await Assert.ThrowsExactlyAsync<FormatException>(async () => await Collect("mssql_product_version", [["nope"]]));

    [TestMethod]
    public async Task MssqlInstanceLocalTimeRecordsEpochSeconds() =>
        StringAssert.Contains(await Collect("mssql_instance_local_time"), "mssql_instance_local_time 1700000000\n");

    [TestMethod]
    public async Task MssqlConnectionsRecordsOneSeriesPerDatabase()
    {
        var text = await Collect("mssql_connections");
        StringAssert.Contains(text, "mssql_connections{database=\"master\",state=\"current\"} 5\n");
        StringAssert.Contains(text, "mssql_connections{database=\"tempdb\",state=\"current\"} 2\n");
    }

    [TestMethod]
    public async Task MssqlClientConnectionsLabelsByClientAndDatabase() =>
        StringAssert.Contains(await Collect("mssql_client_connections"), "mssql_client_connections{client=\"apphost\",database=\"master\"} 3\n");

    [TestMethod]
    public async Task MultiRowCollectorsTolerateAnEmptyRowSet()
    {
        var text = await Collect("mssql_connections", []);
        Assert.DoesNotContain("mssql_connections{", text);
    }

    [TestMethod]
    public async Task MssqlDeadlocksUsesTheShortGaugeName() =>
        StringAssert.Contains(await Collect("mssql_deadlocks"), "mssql_deadlocks 0\n");

    [TestMethod]
    public async Task MssqlTransactionsLabelsByDatabase()
    {
        var text = await Collect("mssql_transactions");
        StringAssert.Contains(text, "mssql_transactions{database=\"tempdb\"} 42\n");
        StringAssert.Contains(text, "mssql_transactions{database=\"master\"} 5\n");
    }

    [TestMethod]
    public async Task MssqlAccessMethodsMapsEachCounterToItsGauge()
    {
        var text = await Collect("mssql_access_methods");
        StringAssert.Contains(text, "mssql_page_splits_total 9\n");
        StringAssert.Contains(text, "mssql_forwarded_records_total 2\n");
    }

    [TestMethod]
    public async Task CounterCollectorsReportZeroForMissingCounters()
    {
        var text = await Collect("mssql_lock_stats", [["Lock Waits/sec", 6]]);
        StringAssert.Contains(text, "mssql_lock_waits_total 6\n");
        StringAssert.Contains(text, "mssql_lock_wait_time_ms_total 0\n");
        StringAssert.Contains(text, "mssql_lock_timeouts_total 0\n");
    }

    [TestMethod]
    public void PerfCounterQueriesKeepTheExactInClauseText()
    {
        Assert.AreEqual(
            "SELECT RTRIM(counter_name), cntr_value\nFROM sys.dm_os_performance_counters\n"
            + "WHERE object_name LIKE '%Access Methods%' AND counter_name IN ('Page Splits/sec', 'Full Scans/sec', 'Range Scans/sec', 'Index Searches/sec', 'Forwarded Records/sec')",
            Get("mssql_access_methods").Query.ReplaceLineEndings("\n"));
    }

    [TestMethod]
    public void NoCollectorIsOptionalYet() => Assert.IsFalse(All.Any(c => c.Optional));
}
