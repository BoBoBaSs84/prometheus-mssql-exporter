using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Per-database state and file sizes.</summary>
internal static class Databases
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_database_state",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_database_state"] = new(
                    "Databases states: 0=ONLINE 1=RESTORING 2=RECOVERING 3=RECOVERY_PENDING 4=SUSPECT 5=EMERGENCY 6=OFFLINE 7=COPYING 10=OFFLINE_SECONDARY",
                    LabelNames: ["database"]),
            },
            "SELECT [d].[name], [d].[state] FROM [master].[sys].[databases] AS [d];",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_database_state", CollectorUtil.AsDouble(row[1]), ("database", CollectorUtil.AsString(row[0])));
                }
            }),

        new(
            "mssql_log_growths",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_log_growths"] = new(
                    "Total number of times the transaction log for the database has been expanded last restart",
                    LabelNames: ["database"]),
            },
            """
SELECT RTRIM([dopc].[instance_name]) AS [instance_name]
	 , [dopc].[cntr_value]
FROM [sys].[dm_os_performance_counters] AS [dopc]
WHERE [dopc].[counter_name] = 'Log Growths'
	AND [dopc].[instance_name] <> '_Total';
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_log_growths", CollectorUtil.AsDouble(row[1]), ("database", CollectorUtil.AsString(row[0])));
                }
            }),

        new(
            "mssql_database_filesize",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_database_filesize"] = new(
                    "Physical sizes of files used by database in KB, their names and types (0=rows, 1=log, 2=filestream,3=n/a 4=fulltext(before v2008 of MSSQL))",
                    LabelNames: ["database", "logicalname", "type", "filename"]),
            },
            """
SELECT DB_NAME([mf].[database_id]) AS [database_name]
	 , [mf].[name] AS [logical_name]
	 , [mf].[type]
	 , [mf].[physical_name]
	 , ([mf].[size] * CAST(8 AS BIGINT)) size_kb
FROM [sys].[master_files] AS [mf];
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set(
                        "mssql_database_filesize",
                        CollectorUtil.AsDouble(row[4]),
                        ("database", CollectorUtil.AsString(row[0])),
                        ("logicalname", CollectorUtil.AsString(row[1])),
                        ("type", CollectorUtil.AsString(row[2])),
                        ("filename", CollectorUtil.AsString(row[3])));
                }
            }),
    ];
}
