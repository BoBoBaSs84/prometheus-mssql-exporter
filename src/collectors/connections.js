import createDebug from "debug";

const log = createDebug("metrics");

/** Active connection counts. */
export default [
  {
    name: "mssql_connections",
    gauges: {
      mssql_connections: { help: "Number of active connections", labelNames: ["database", "state"] },
    },
    query: `SELECT DB_NAME([sp].[dbid]), COUNT([sp].[spid]) FROM [sys].[sysprocesses] AS [sp] GROUP BY DB_NAME([sp].[dbid]);`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const database = row[0].value;
        const count = row[1].value;
        log("Fetched number of connections for database", database, count);
        metrics.mssql_connections.set({ database, state: "current" }, count);
      }
    },
  },
  {
    name: "mssql_client_connections",
    gauges: {
      mssql_client_connections: { help: "Number of active client connections", labelNames: ["client", "database"] },
    },
    query: `SELECT host_name AS [host_name]
	 , DB_NAME([s].[dbid]) AS [dbname]
	 , COUNT(*) AS [session_count]
FROM [sys].[dm_exec_sessions] AS [des]
LEFT JOIN [sys].[sysprocesses] AS [s]
	ON [des].[session_id] = [s].[spid]
WHERE [des].[is_user_process] = 1
GROUP BY [host_name]
	   , [s].[dbid];`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const client = row[0].value;
        const database = row[1].value;
        const count = row[2].value;
        log("Fetched number of connections for client", client, database, count);
        metrics.mssql_client_connections.set({ client, database }, count);
      }
    },
  },
];
