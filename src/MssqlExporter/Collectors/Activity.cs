using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Live request / blocking / transaction activity (single-row aggregates).</summary>
internal static class Activity
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_activity",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_blocked_sessions"] = new("Number of requests currently blocked by another session"),
                ["mssql_blocking_wait_seconds_max"] = new("Longest current wait among blocked requests, in seconds"),
                ["mssql_active_requests"] = new("Number of user requests currently executing (session_id > 50)"),
                ["mssql_longest_running_request_seconds"] = new("Elapsed time of the longest-running user request, in seconds"),
                ["mssql_oldest_active_transaction_seconds"] = new("Age of the oldest active transaction, in seconds"),
            },
            """
SELECT
  (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0),
  (SELECT ISNULL(MAX(wait_time), 0) / 1000.0 FROM sys.dm_exec_requests WHERE blocking_session_id <> 0),
  (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id > 50),
  (SELECT ISNULL(MAX(total_elapsed_time), 0) / 1000.0 FROM sys.dm_exec_requests WHERE session_id > 50),
  (SELECT ISNULL(DATEDIFF(SECOND, MIN(dt.database_transaction_begin_time), GETDATE()), 0)
     FROM sys.dm_tran_database_transactions dt
     JOIN sys.dm_tran_session_transactions st ON st.transaction_id = dt.transaction_id
     WHERE dt.database_transaction_begin_time IS NOT NULL)
""",
            (rows, gauges) =>
            {
                var row = rows[0];
                gauges.Set("mssql_blocked_sessions", CollectorUtil.AsDouble(row[0]));
                gauges.Set("mssql_blocking_wait_seconds_max", CollectorUtil.AsDouble(row[1]));
                gauges.Set("mssql_active_requests", CollectorUtil.AsDouble(row[2]));
                gauges.Set("mssql_longest_running_request_seconds", CollectorUtil.AsDouble(row[3]));
                gauges.Set("mssql_oldest_active_transaction_seconds", CollectorUtil.AsDouble(row[4]));
            }),

        new(
            "mssql_active_sessions",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_active_sessions"] = new("Number of user sessions by status", LabelNames: ["status"]),
            },
            "SELECT RTRIM(status), COUNT(*) FROM sys.dm_exec_sessions WHERE session_id > 50 GROUP BY status",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_active_sessions", CollectorUtil.AsDouble(row[1]), ("status", CollectorUtil.AsString(row[0])));
                }
            }),
    ];
}
