import createDebug from "debug";

const log = createDebug("metrics");

/**
 * Backup recency per database and backup type. Requires read access to
 * msdb.dbo.backupset (see README "Required permissions").
 *
 * `age_seconds` is -1 when no backup of that type has ever been taken.
 */
export default [
  {
    name: "mssql_backups",
    gauges: {
      mssql_database_backup_age_seconds: {
        help: "Seconds since the most recent backup of this type (-1 = never)",
        labelNames: ["database", "type"],
      },
      mssql_database_last_backup_timestamp: {
        help: "Unix timestamp of the most recent backup of this type (0 = never)",
        labelNames: ["database", "type"],
      },
    },
    query: `SELECT d.name, bt.[type],
  DATEDIFF(SECOND, MAX(b.backup_finish_date), GETDATE()),
  DATEDIFF(SECOND, '19700101', MAX(b.backup_finish_date))
FROM sys.databases d
CROSS JOIN (VALUES ('D', 'full'), ('I', 'diff'), ('L', 'log')) AS bt(code, [type])
LEFT JOIN msdb.dbo.backupset b ON b.database_name = d.name AND b.[type] = bt.code
WHERE d.database_id <> 2
GROUP BY d.name, bt.[type]`,
    collect: (rows, metrics) => {
      log("Fetched backup history rows", rows.length);
      for (const row of rows) {
        const database = row[0].value;
        const type = row[1].value;
        const age = row[2].value == null ? -1 : row[2].value;
        const timestamp = row[3].value == null ? 0 : row[3].value;
        metrics.mssql_database_backup_age_seconds.set({ database, type }, age);
        metrics.mssql_database_last_backup_timestamp.set({ database, type }, timestamp);
      }
    },
  },
];
