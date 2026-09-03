import createDebug from "debug";

const log = createDebug("metrics");

/** Live request / blocking / transaction activity (single-row aggregates). */
export default [
  {
    name: "mssql_activity",
    gauges: {
      mssql_blocked_sessions: { help: "Number of requests currently blocked by another session" },
      mssql_blocking_wait_seconds_max: { help: "Longest current wait among blocked requests, in seconds" },
      mssql_active_requests: { help: "Number of user requests currently executing (session_id > 50)" },
      mssql_longest_running_request_seconds: { help: "Elapsed time of the longest-running user request, in seconds" },
      mssql_oldest_active_transaction_seconds: { help: "Age of the oldest active transaction, in seconds" },
    },
    query: `SELECT
  (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id <> 0),
  (SELECT ISNULL(MAX(wait_time), 0) / 1000.0 FROM sys.dm_exec_requests WHERE blocking_session_id <> 0),
  (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE session_id > 50),
  (SELECT ISNULL(MAX(total_elapsed_time), 0) / 1000.0 FROM sys.dm_exec_requests WHERE session_id > 50),
  (SELECT ISNULL(DATEDIFF(SECOND, MIN(dt.database_transaction_begin_time), GETDATE()), 0)
     FROM sys.dm_tran_database_transactions dt
     JOIN sys.dm_tran_session_transactions st ON st.transaction_id = dt.transaction_id
     WHERE dt.database_transaction_begin_time IS NOT NULL)`,
    collect: (rows, metrics) => {
      const [blocked, blockingWait, active, longest, oldestTxn] = rows[0].map((cell) => cell.value);
      log("Fetched activity", blocked, active);
      metrics.mssql_blocked_sessions.set(blocked);
      metrics.mssql_blocking_wait_seconds_max.set(blockingWait);
      metrics.mssql_active_requests.set(active);
      metrics.mssql_longest_running_request_seconds.set(longest);
      metrics.mssql_oldest_active_transaction_seconds.set(oldestTxn);
    },
  },
  {
    name: "mssql_active_sessions",
    gauges: {
      mssql_active_sessions: { help: "Number of user sessions by status", labelNames: ["status"] },
    },
    query: `SELECT RTRIM(status), COUNT(*) FROM sys.dm_exec_sessions WHERE session_id > 50 GROUP BY status`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        metrics.mssql_active_sessions.set({ status: row[0].value }, row[1].value);
      }
    },
  },
];
