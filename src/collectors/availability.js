import createDebug from "debug";

const log = createDebug("metrics");

/** Per-database configuration flags and integrity signals. */
export default [
  {
    name: "mssql_database_properties",
    gauges: {
      mssql_database_recovery_model: { help: "Recovery model: 1=FULL 2=BULK_LOGGED 3=SIMPLE", labelNames: ["database"] },
      mssql_database_is_read_only: { help: "1 if the database is read-only", labelNames: ["database"] },
      mssql_database_is_auto_close_on: { help: "1 if AUTO_CLOSE is enabled", labelNames: ["database"] },
      mssql_database_is_auto_shrink_on: { help: "1 if AUTO_SHRINK is enabled", labelNames: ["database"] },
      mssql_database_page_verify_option: { help: "Page verify: 0=NONE 1=TORN_PAGE_DETECTION 2=CHECKSUM", labelNames: ["database"] },
    },
    query: `SELECT name, recovery_model, CAST(is_read_only AS INT), CAST(is_auto_close_on AS INT), CAST(is_auto_shrink_on AS INT), page_verify_option
FROM sys.databases`,
    collect: (rows, metrics) => {
      log("Fetched database properties", rows.length);
      for (const row of rows) {
        const database = row[0].value;
        metrics.mssql_database_recovery_model.set({ database }, row[1].value);
        metrics.mssql_database_is_read_only.set({ database }, row[2].value);
        metrics.mssql_database_is_auto_close_on.set({ database }, row[3].value);
        metrics.mssql_database_is_auto_shrink_on.set({ database }, row[4].value);
        metrics.mssql_database_page_verify_option.set({ database }, row[5].value);
      }
    },
  },
  {
    name: "mssql_suspect_pages",
    gauges: {
      mssql_suspect_pages: { help: "Number of rows in msdb.dbo.suspect_pages for the database", labelNames: ["database"] },
    },
    query: `SELECT d.name, COUNT(sp.file_id)
FROM sys.databases d
LEFT JOIN msdb.dbo.suspect_pages sp ON sp.database_id = d.database_id
GROUP BY d.name`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        metrics.mssql_suspect_pages.set({ database: row[0].value }, row[1].value);
      }
    },
  },
];
