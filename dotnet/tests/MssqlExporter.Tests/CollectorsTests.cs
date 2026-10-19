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
    public async Task MssqlDatabaseStateRecordsTheStatePerDatabase() =>
        StringAssert.Contains(await Collect("mssql_database_state"), "mssql_database_state{database=\"master\"} 0\n");

    [TestMethod]
    public async Task MssqlDatabaseFilesizeCarriesAllFourLabels() =>
        StringAssert.Contains(
            await Collect("mssql_database_filesize"),
            "mssql_database_filesize{database=\"master\",logicalname=\"master\",type=\"0\",filename=\"/var/opt/mssql/data/master.mdf\"} 4096\n");

    [TestMethod]
    public async Task MssqlBufferManagerSplitsThePivotedRowIntoGauges()
    {
        var text = await Collect("mssql_buffer_manager");
        StringAssert.Contains(text, "mssql_page_read_total 100\n");
        StringAssert.Contains(text, "mssql_page_write_total 50\n");
        StringAssert.Contains(text, "mssql_page_life_expectancy 8000\n");
        StringAssert.Contains(text, "mssql_lazy_write_total 3\n");
        StringAssert.Contains(text, "mssql_page_checkpoint_total 12\n");
    }

    [TestMethod]
    public async Task MssqlIoStallRecordsReadWriteQueuedTypesPlusTheTotal()
    {
        var text = await Collect("mssql_io_stall");
        StringAssert.Contains(text, "mssql_io_stall{database=\"master\",type=\"read\"} 10\n");
        StringAssert.Contains(text, "mssql_io_stall{database=\"master\",type=\"write\"} 20\n");
        StringAssert.Contains(text, "mssql_io_stall{database=\"master\",type=\"queued_read\"} 1\n");
        StringAssert.Contains(text, "mssql_io_stall{database=\"master\",type=\"queued_write\"} 2\n");
        StringAssert.Contains(text, "mssql_io_stall_total{database=\"master\"} 35\n");
    }

    [TestMethod]
    public async Task MssqlOsSysMemoryRecordsEachMemoryGauge()
    {
        var text = await Collect("mssql_os_sys_memory");
        StringAssert.Contains(text, "mssql_total_physical_memory_kb 1000\n");
        StringAssert.Contains(text, "mssql_available_physical_memory_kb 400\n");
        StringAssert.Contains(text, "mssql_total_page_file_kb 2000\n");
        StringAssert.Contains(text, "mssql_available_page_file_kb 800\n");
    }

    [TestMethod]
    public async Task MssqlMemoryManagerConvertsKbCountersToBytes()
    {
        var text = await Collect("mssql_memory_manager");
        StringAssert.Contains(text, $"mssql_total_server_memory_bytes {1_500_000L * 1024}\n");
        StringAssert.Contains(text, "mssql_memory_grants_pending 0\n");
        StringAssert.Contains(text, "mssql_memory_grants_outstanding 3\n");
    }

    [TestMethod]
    public async Task MssqlCacheHitRatioComputesRatioOverBasePercentages()
    {
        var text = await Collect("mssql_cache_hit_ratio");
        StringAssert.Contains(text, "mssql_buffer_cache_hit_ratio 99\n");
        StringAssert.Contains(text, "mssql_plan_cache_hit_ratio 90\n");
    }

    [TestMethod]
    public async Task MssqlActivityReadsTheSingleAggregateRow()
    {
        var text = await Collect("mssql_activity");
        StringAssert.Contains(text, "mssql_blocked_sessions 2\n");
        StringAssert.Contains(text, "mssql_blocking_wait_seconds_max 1.5\n");
        StringAssert.Contains(text, "mssql_oldest_active_transaction_seconds 30\n");
    }

    [TestMethod]
    public async Task MssqlWaitStatsLabelsByWaitType()
    {
        var text = await Collect("mssql_wait_stats");
        StringAssert.Contains(text, "mssql_wait_time_ms{wait_type=\"PAGEIOLATCH_SH\"} 5000\n");
        StringAssert.Contains(text, "mssql_signal_wait_time_ms{wait_type=\"LCK_M_X\"} 60\n");
    }

    [TestMethod]
    public void WaitQueriesExcludeBenignWaits()
    {
        StringAssert.Contains(Get("mssql_wait_stats").Query, "NOT IN ('BROKER_EVENTHANDLER', 'BROKER_RECEIVE_WAITFOR'");
        StringAssert.Contains(Get("mssql_wait_time_total").Query, "'XE_DISPATCHER_WAIT', 'XE_TIMER_EVENT')");
    }

    [TestMethod]
    public async Task MssqlLogSpaceConvertsMbToBytes()
    {
        var text = await Collect("mssql_log_space");
        StringAssert.Contains(text, $"mssql_database_log_size_bytes{{database=\"master\"}} {8 * 1024 * 1024}\n");
        StringAssert.Contains(text, "mssql_database_log_used_percent{database=\"master\"} 12.5\n");
    }

    [TestMethod]
    public async Task MssqlBackupsReportsMinusOneAgeAndZeroTimestampWhenNeverTaken()
    {
        var text = await Collect("mssql_backups");
        StringAssert.Contains(text, "mssql_database_backup_age_seconds{database=\"master\",type=\"full\"} 3600\n");
        StringAssert.Contains(text, "mssql_database_backup_age_seconds{database=\"master\",type=\"diff\"} -1\n");
        StringAssert.Contains(text, "mssql_database_last_backup_timestamp{database=\"master\",type=\"log\"} 0\n");
    }

    [TestMethod]
    public async Task MssqlHadrDatabaseCarriesAgReplicaAndDatabaseLabels() =>
        StringAssert.Contains(
            await Collect("mssql_hadr_database"),
            "mssql_hadr_secondary_lag_seconds{ag=\"ag1\",replica=\"NODE2\",database=\"appdb\"} 1\n");

    [TestMethod]
    public async Task MssqlAgentJobsRecordsPerJobOutcome()
    {
        var text = await Collect("mssql_agent_jobs");
        StringAssert.Contains(text, "mssql_agent_job_last_run_success{job=\"Nightly Backup\"} 1\n");
        StringAssert.Contains(text, "mssql_agent_job_last_duration_seconds{job=\"Nightly Backup\"} 125\n");
    }

    [TestMethod]
    public void OptionalCollectorsAreFlagged() =>
        CollectionAssert.AreEqual(
            new[] { "mssql_hadr_replica", "mssql_hadr_database", "mssql_agent_up", "mssql_agent_jobs" },
            All.Where(c => c.Optional).Select(c => c.Name).ToArray());

    [TestMethod]
    public void EveryCollectorDeclaresAQueryAndAtLeastOneGauge()
    {
        foreach (var collector in All)
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(collector.Query), collector.Name);
            Assert.IsNotEmpty(collector.Gauges, collector.Name);
        }
    }

    [TestMethod]
    public void CollectorsAreRegisteredInTheNodeOrder() =>
        CollectionAssert.AreEqual(
            new[]
            {
                "mssql_up", "mssql_product_version", "mssql_instance_local_time",
                "mssql_connections", "mssql_client_connections",
                "mssql_user_errors", "mssql_kill_connection_errors",
                "mssql_database_state", "mssql_log_growths", "mssql_database_filesize",
                "mssql_buffer_manager", "mssql_io_stall",
            },
            All.Take(12).Select(c => c.Name).ToArray());
}
