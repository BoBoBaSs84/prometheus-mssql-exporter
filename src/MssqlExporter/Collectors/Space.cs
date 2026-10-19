using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>Transaction-log space, VLF counts and volume free space.</summary>
internal static class Space
{
    private const double Mb = 1024 * 1024;

    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_log_space",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_database_log_size_bytes"] = new("Configured transaction-log size per database", LabelNames: ["database"]),
                ["mssql_database_log_used_percent"] = new("Percentage of the transaction log currently in use", LabelNames: ["database"]),
            },
            "DBCC SQLPERF(LOGSPACE) WITH NO_INFOMSGS",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    var database = ("database", CollectorUtil.AsString(row[0]));
                    gauges.Set("mssql_database_log_size_bytes", CollectorUtil.AsDouble(row[1]) * Mb, database);
                    gauges.Set("mssql_database_log_used_percent", CollectorUtil.AsDouble(row[2]), database);
                }
            }),

        new(
            "mssql_vlf_count",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_database_vlf_count"] = new("Number of virtual log files in the transaction log", LabelNames: ["database"]),
            },
            """
SELECT DB_NAME(d.database_id), COUNT(li.database_id)
FROM sys.databases d
OUTER APPLY sys.dm_db_log_info(d.database_id) li
WHERE d.state = 0
GROUP BY d.database_id
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    gauges.Set("mssql_database_vlf_count", CollectorUtil.AsDouble(row[1]), ("database", CollectorUtil.AsString(row[0])));
                }
            }),

        new(
            "mssql_volume_stats",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_volume_total_bytes"] = new("Total size of a volume that hosts database files", LabelNames: ["volume"]),
                ["mssql_volume_available_bytes"] = new("Free space on a volume that hosts database files", LabelNames: ["volume"]),
            },
            """
SELECT DISTINCT vs.volume_mount_point, vs.total_bytes, vs.available_bytes
FROM sys.master_files mf
CROSS APPLY sys.dm_os_volume_stats(mf.database_id, mf.file_id) vs
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    var volume = ("volume", CollectorUtil.AsString(row[0]));
                    gauges.Set("mssql_volume_total_bytes", CollectorUtil.AsDouble(row[1]), volume);
                    gauges.Set("mssql_volume_available_bytes", CollectorUtil.AsDouble(row[2]), volume);
                }
            }),
    ];
}
