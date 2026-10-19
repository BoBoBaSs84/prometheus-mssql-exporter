using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Server error-rate performance counters.</summary>
internal static class Errors
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_user_errors",
            new Dictionary<string, GaugeSpec> { ["mssql_user_errors"] = new("Number of user errors/sec since last restart") },
            """
SELECT cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Errors/sec' AND instance_name = 'User Errors'
""",
            (rows, gauges) => gauges.Set("mssql_user_errors", CollectorUtil.AsDouble(rows[0][0]))),

        new(
            "mssql_kill_connection_errors",
            new Dictionary<string, GaugeSpec> { ["mssql_kill_connection_errors"] = new("Number of kill connection errors/sec since last restart") },
            """
SELECT cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Errors/sec' AND instance_name = 'Kill Connection Errors'
""",
            (rows, gauges) => gauges.Set("mssql_kill_connection_errors", CollectorUtil.AsDouble(rows[0][0]))),
    ];
}
