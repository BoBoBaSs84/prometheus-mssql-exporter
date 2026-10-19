using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>
/// Backup recency per database and backup type. Requires read access to
/// msdb.dbo.backupset (see README "Required permissions").
/// <c>age_seconds</c> is -1 when no backup of that type has ever been taken.
/// </summary>
internal static class Backups
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_backups",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_database_backup_age_seconds"] = new(
                    "Seconds since the most recent backup of this type (-1 = never)",
                    LabelNames: ["database", "type"]),
                ["mssql_database_last_backup_timestamp"] = new(
                    "Unix timestamp of the most recent backup of this type (0 = never)",
                    LabelNames: ["database", "type"]),
            },
            """
SELECT d.name, bt.[type],
  DATEDIFF(SECOND, MAX(b.backup_finish_date), GETDATE()),
  DATEDIFF(SECOND, '19700101', MAX(b.backup_finish_date))
FROM sys.databases d
CROSS JOIN (VALUES ('D', 'full'), ('I', 'diff'), ('L', 'log')) AS bt(code, [type])
LEFT JOIN msdb.dbo.backupset b ON b.database_name = d.name AND b.[type] = bt.code
WHERE d.database_id <> 2
GROUP BY d.name, bt.[type]
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    (string, string)[] labels = [("database", CollectorUtil.AsString(row[0])), ("type", CollectorUtil.AsString(row[1]))];
                    gauges.Set("mssql_database_backup_age_seconds", CollectorUtil.AsDouble(row[2]) ?? -1, labels);
                    gauges.Set("mssql_database_last_backup_timestamp", CollectorUtil.AsDouble(row[3]) ?? 0, labels);
                }
            }),
    ];
}
