namespace MssqlExporter.Tests.Fixtures;

/// <summary>
/// Fixture rows, one entry per collector, in SELECT column order (as materialised by <c>ISqlSession</c>).
/// Port of <c>test/fixtures/rows.js</c>; <c>CollectorsTests</c> asserts the keys equal the collector names exactly.
/// </summary>
internal static class Rows
{
    private static object?[] R(params object?[] values) => values;

    public static IReadOnlyDictionary<string, object?[][]> All { get; } = new Dictionary<string, object?[][]>
    {
        // instance
        ["mssql_up"] = [R(1)],
        ["mssql_product_version"] = [R("15.0.2000.5")],
        ["mssql_instance_local_time"] = [R(1_700_000_000)],

        // connections
        ["mssql_connections"] = [R("master", 5), R("tempdb", 2)],
        ["mssql_client_connections"] = [R("apphost", "master", 3)],

        // errors
        ["mssql_user_errors"] = [R(7)],
        ["mssql_kill_connection_errors"] = [R(1)],

        // databases
        ["mssql_database_state"] = [R("master", 0), R("tempdb", 0)],
        ["mssql_log_growths"] = [R("tempdb", 2), R("master", 0)],
        ["mssql_database_filesize"] = [R("master", "master", 0, "/var/opt/mssql/data/master.mdf", 4096)],

        // io
        ["mssql_buffer_manager"] = [R(100, 50, 8000, 3, 12)],
        ["mssql_io_stall"] = [R("master", 10, 20, 35, 1, 2)],

        // throughput
        ["mssql_deadlocks"] = [R(0)],
        ["mssql_batch_requests"] = [R(500)],
        ["mssql_transactions"] = [R("tempdb", 42), R("master", 5)],
        ["mssql_sql_statistics"] = [R("SQL Compilations/sec", 111), R("SQL Re-Compilations/sec", 4)],
        ["mssql_access_methods"] =
        [
            R("Page Splits/sec", 9),
            R("Full Scans/sec", 21),
            R("Range Scans/sec", 88),
            R("Index Searches/sec", 1200),
            R("Forwarded Records/sec", 2),
        ],
        ["mssql_lock_stats"] = [R("Lock Waits/sec", 6), R("Lock Wait Time (ms)", 250), R("Number of Lock Timeouts/sec", 1)],
        ["mssql_latch_stats"] = [R("Latch Waits/sec", 15), R("Total Latch Wait Time (ms)", 300)],

        // memory
        ["mssql_os_process_memory"] = [R(123, 45)],
        ["mssql_os_sys_memory"] = [R(1000, 400, 2000, 800)],
        ["mssql_memory_manager"] =
        [
            R("Target Server Memory (KB)", 2_000_000),
            R("Total Server Memory (KB)", 1_500_000),
            R("Memory Grants Pending", 0),
            R("Memory Grants Outstanding", 3),
            R("Granted Workspace Memory (KB)", 40_000),
            R("Maximum Workspace Memory (KB)", 900_000),
            R("Connection Memory (KB)", 5_000),
            R("Lock Memory (KB)", 2_000),
            R("Optimizer Memory (KB)", 1_000),
            R("SQL Cache Memory (KB)", 8_000),
        ],
        ["mssql_cache_hit_ratio"] = [R("Buffer cache hit ratio", 990), R("Buffer cache hit ratio base", 1000), R("Cache Hit Ratio", 45), R("Cache Hit Ratio Base", 50)],

        // activity
        ["mssql_activity"] = [R(2, 1.5m, 4, 12.5m, 30)],
        ["mssql_active_sessions"] = [R("running", 1), R("sleeping", 3)],

        // waits
        ["mssql_wait_stats"] = [R("PAGEIOLATCH_SH", 5000, 120, 200), R("LCK_M_X", 1200, 8, 60)],
        ["mssql_wait_time_total"] = [R(6200)],

        // space
        ["mssql_log_space"] = [R("master", 8.0, 12.5, 0), R("tempdb", 16.0, 3.0, 0)],
        ["mssql_vlf_count"] = [R("master", 4), R("tempdb", 8)],
        ["mssql_volume_stats"] = [R("/var/opt/mssql", 50_000_000_000L, 20_000_000_000L)],

        // settings
        ["mssql_configuration"] = [R("max degree of parallelism", 0), R("cost threshold for parallelism", 5)],

        // backups
        ["mssql_backups"] = [R("master", "full", 3600, 1_699_996_400), R("master", "diff", null, null), R("master", "log", null, null)],

        // availability
        ["mssql_database_properties"] = [R("master", 3, 0, 0, 0, 2), R("model", 1, 0, 0, 0, 2)],
        ["mssql_suspect_pages"] = [R("master", 0), R("tempdb", 0)],

        // always on
        ["mssql_hadr_enabled"] = [R(0)],
        ["mssql_hadr_replica"] = [R("ag1", "NODE1", 2, 1, 2)],
        ["mssql_hadr_database"] = [R("ag1", "NODE2", "appdb", 2, 2, 0, 0, 1)],

        // agent
        ["mssql_agent_up"] = [R(1)],
        ["mssql_agent_jobs"] = [R("Nightly Backup", 1, 1, 1_699_990_000, 125, 0)],
    };
}
