using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Active connection counts.</summary>
internal static class Connections
{
    private const string ClientConnectionsSql = """
SELECT host_name AS [host_name]
	 , DB_NAME([s].[dbid]) AS [dbname]
	 , COUNT(*) AS [session_count]
FROM [sys].[dm_exec_sessions] AS [des]
LEFT JOIN [sys].[sysprocesses] AS [s]
	ON [des].[session_id] = [s].[spid]
WHERE [des].[is_user_process] = 1
GROUP BY [host_name]
	   , [s].[dbid];
""";

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_connections",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_connections"] = new("Number of active connections", LabelNames: ["database", "state"]),
            },
            "SELECT DB_NAME([sp].[dbid]), COUNT([sp].[spid]) FROM [sys].[sysprocesses] AS [sp] GROUP BY DB_NAME([sp].[dbid]);",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set(
                        "mssql_connections",
                        CollectorUtil.AsDouble(row[1]),
                        ("database", CollectorUtil.AsString(row[0])),
                        ("state", "current"));
                }
            }),

        new(
            "mssql_client_connections",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_client_connections"] = new("Number of active client connections", LabelNames: ["client", "database"]),
            },
            ClientConnectionsSql,
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set(
                        "mssql_client_connections",
                        CollectorUtil.AsDouble(row[2]),
                        ("client", CollectorUtil.AsString(row[0])),
                        ("database", CollectorUtil.AsString(row[1])));
                }
            }),
    ];
}
