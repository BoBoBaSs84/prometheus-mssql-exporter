import createDebug from "debug";

const log = createDebug("metrics");

/** Per-database state and file sizes. */
export default [
  {
    name: "mssql_database_state",
    gauges: {
      mssql_database_state: {
        help: "Databases states: 0=ONLINE 1=RESTORING 2=RECOVERING 3=RECOVERY_PENDING 4=SUSPECT 5=EMERGENCY 6=OFFLINE 7=COPYING 10=OFFLINE_SECONDARY",
        labelNames: ["database"],
      },
    },
    query: `SELECT [d].[name], [d].[state] FROM [master].[sys].[databases] AS [d];`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const database = row[0].value;
        const state = row[1].value;
        log("Fetched state for database", database, state);
        metrics.mssql_database_state.set({ database }, state);
      }
    },
  },
  {
    name: "mssql_log_growths",
    gauges: {
      mssql_log_growths: {
        help: "Total number of times the transaction log for the database has been expanded last restart",
        labelNames: ["database"],
      },
    },
    query: `SELECT RTRIM([dopc].[instance_name]) AS [instance_name]
	 , [dopc].[cntr_value]
FROM [sys].[dm_os_performance_counters] AS [dopc]
WHERE [dopc].[counter_name] = 'Log Growths'
	AND [dopc].[instance_name] <> '_Total';`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const database = row[0].value;
        const value = row[1].value;
        log("Fetched number log growths for database", database, value);
        metrics.mssql_log_growths.set({ database }, value);
      }
    },
  },
  {
    name: "mssql_database_filesize",
    gauges: {
      mssql_database_filesize: {
        help: "Physical sizes of files used by database in KB, their names and types (0=rows, 1=log, 2=filestream,3=n/a 4=fulltext(before v2008 of MSSQL))",
        labelNames: ["database", "logicalname", "type", "filename"],
      },
    },
    query: `SELECT DB_NAME([mf].[database_id]) AS [database_name]
	 , [mf].[name] AS [logical_name]
	 , [mf].[type]
	 , [mf].[physical_name]
	 , ([mf].[size] * CAST(8 AS BIGINT)) size_kb
FROM [sys].[master_files] AS [mf];`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const database = row[0].value;
        const logicalname = row[1].value;
        const type = row[2].value;
        const filename = row[3].value;
        const sizeKb = row[4].value;
        log("Fetched size of files for database", database, logicalname, type, filename, sizeKb);
        metrics.mssql_database_filesize.set({ database, logicalname, type, filename }, sizeKb);
      }
    },
  },
];
