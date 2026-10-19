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
    };
}
