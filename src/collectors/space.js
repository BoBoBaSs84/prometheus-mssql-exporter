import createDebug from "debug";

const log = createDebug("metrics");

const MB = 1024 * 1024;

/** Transaction-log space, VLF counts and volume free space. */
export default [
  {
    name: "mssql_log_space",
    gauges: {
      mssql_database_log_size_bytes: { help: "Configured transaction-log size per database", labelNames: ["database"] },
      mssql_database_log_used_percent: { help: "Percentage of the transaction log currently in use", labelNames: ["database"] },
    },
    query: `DBCC SQLPERF(LOGSPACE) WITH NO_INFOMSGS`,
    collect: (rows, metrics) => {
      log("Fetched log space rows", rows.length);
      for (const row of rows) {
        const database = row[0].value;
        metrics.mssql_database_log_size_bytes.set({ database }, Number(row[1].value) * MB);
        metrics.mssql_database_log_used_percent.set({ database }, Number(row[2].value));
      }
    },
  },
  {
    name: "mssql_vlf_count",
    gauges: {
      mssql_database_vlf_count: { help: "Number of virtual log files in the transaction log", labelNames: ["database"] },
    },
    query: `SELECT DB_NAME(d.database_id), COUNT(li.database_id)
FROM sys.databases d
OUTER APPLY sys.dm_db_log_info(d.database_id) li
WHERE d.state = 0
GROUP BY d.database_id`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        metrics.mssql_database_vlf_count.set({ database: row[0].value }, row[1].value);
      }
    },
  },
  {
    name: "mssql_volume_stats",
    gauges: {
      mssql_volume_total_bytes: { help: "Total size of a volume that hosts database files", labelNames: ["volume"] },
      mssql_volume_available_bytes: { help: "Free space on a volume that hosts database files", labelNames: ["volume"] },
    },
    query: `SELECT DISTINCT vs.volume_mount_point, vs.total_bytes, vs.available_bytes
FROM sys.master_files mf
CROSS APPLY sys.dm_os_volume_stats(mf.database_id, mf.file_id) vs`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const volume = row[0].value;
        metrics.mssql_volume_total_bytes.set({ volume }, row[1].value);
        metrics.mssql_volume_available_bytes.set({ volume }, row[2].value);
      }
    },
  },
];
