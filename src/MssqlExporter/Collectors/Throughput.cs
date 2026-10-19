using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Throughput and contention performance counters (cumulative, use rate()).</summary>
internal static class Throughput
{
    // counter_name -> gauge name, for the perf-counter batch collectors below. Order matters: it drives the IN (...) clause.
    private static readonly (string Counter, string Metric)[] AccessMethods =
    [
        ("Page Splits/sec", "mssql_page_splits_total"),
        ("Full Scans/sec", "mssql_full_scans_total"),
        ("Range Scans/sec", "mssql_range_scans_total"),
        ("Index Searches/sec", "mssql_index_searches_total"),
        ("Forwarded Records/sec", "mssql_forwarded_records_total"),
    ];

    private static readonly (string Counter, string Metric)[] SqlStatistics =
    [
        ("SQL Compilations/sec", "mssql_sql_compilations_total"),
        ("SQL Re-Compilations/sec", "mssql_sql_recompilations_total"),
    ];

    private static readonly (string Counter, string Metric)[] LockStats =
    [
        ("Lock Waits/sec", "mssql_lock_waits_total"),
        ("Lock Wait Time (ms)", "mssql_lock_wait_time_ms_total"),
        ("Number of Lock Timeouts/sec", "mssql_lock_timeouts_total"),
    ];

    private static readonly (string Counter, string Metric)[] LatchStats =
    [
        ("Latch Waits/sec", "mssql_latch_waits_total"),
        ("Total Latch Wait Time (ms)", "mssql_latch_wait_time_ms_total"),
    ];

    private static Action<IReadOnlyList<object?[]>, GaugeSet> CounterCollector((string Counter, string Metric)[] mapping) =>
        (rows, gauges) =>
        {
            var counters = CollectorUtil.ByName(rows);
            foreach (var (counter, metric) in mapping)
            {
                gauges.Set(metric, counters.GetValueOrDefault(counter, 0));
            }
        };

    private static Dictionary<string, GaugeSpec> GaugesFor((string Counter, string Metric)[] mapping, string help) =>
        mapping.ToDictionary(m => m.Metric, _ => new GaugeSpec(help));

    private static string InClause((string Counter, string Metric)[] mapping) =>
        string.Join(", ", mapping.Select(m => $"'{m.Counter}'"));

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_deadlocks",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_deadlocks_per_second"] = new(
                    "Number of lock requests per second that resulted in a deadlock since last restart",
                    Name: "mssql_deadlocks"),
            },
            """
SELECT cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Number of Deadlocks/sec' AND instance_name = '_Total'
""",
            (rows, gauges) => gauges.Set("mssql_deadlocks_per_second", CollectorUtil.AsDouble(rows[0][0]))),

        new(
            "mssql_batch_requests",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_batch_requests"] = new(
                    "Number of Transact-SQL command batches received per second. This statistic is affected by all constraints (such as I/O, number of users, cachesize, complexity of requests, and so on). High batch requests mean good throughput"),
            },
            """
SELECT TOP 1 cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Batch Requests/sec'
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_batch_requests", CollectorUtil.AsDouble(row[0]));
                }
            }),

        new(
            "mssql_transactions",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_transactions"] = new(
                    "Number of transactions started for the database per second. Transactions/sec does not count XTP-only transactions (transactions started by a natively compiled stored procedure.)",
                    LabelNames: ["database"]),
            },
            """
SELECT rtrim(instance_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Transactions/sec' AND instance_name <> '_Total'
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_transactions", CollectorUtil.AsDouble(row[1]), ("database", CollectorUtil.AsString(row[0])));
                }
            }),

        new(
            "mssql_sql_statistics",
            GaugesFor(SqlStatistics, "Cumulative count since last restart (use rate())"),
            $"""
SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%SQL Statistics%' AND counter_name IN ({InClause(SqlStatistics)})
""",
            CounterCollector(SqlStatistics)),

        new(
            "mssql_access_methods",
            GaugesFor(AccessMethods, "Cumulative count since last restart (use rate())"),
            $"""
SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Access Methods%' AND counter_name IN ({InClause(AccessMethods)})
""",
            CounterCollector(AccessMethods)),

        new(
            "mssql_lock_stats",
            GaugesFor(LockStats, "Cumulative lock statistic since last restart, all lock types (use rate())"),
            $"""
SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Locks%' AND instance_name = '_Total' AND counter_name IN ({InClause(LockStats)})
""",
            CounterCollector(LockStats)),

        new(
            "mssql_latch_stats",
            GaugesFor(LatchStats, "Cumulative latch statistic since last restart (use rate())"),
            $"""
SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Latches%' AND counter_name IN ({InClause(LatchStats)})
""",
            CounterCollector(LatchStats)),
    ];
}
