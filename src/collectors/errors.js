import createDebug from "debug";

const log = createDebug("metrics");

/** Server error-rate performance counters. */
export default [
  {
    name: "mssql_user_errors",
    gauges: {
      mssql_user_errors: { help: "Number of user errors/sec since last restart" },
    },
    query: `SELECT cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Errors/sec' AND instance_name = 'User Errors'`,
    collect: (rows, metrics) => {
      const value = rows[0][0].value;
      log("Fetched number of user errors/sec", value);
      metrics.mssql_user_errors.set(value);
    },
  },
  {
    name: "mssql_kill_connection_errors",
    gauges: {
      mssql_kill_connection_errors: { help: "Number of kill connection errors/sec since last restart" },
    },
    query: `SELECT cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Errors/sec' AND instance_name = 'Kill Connection Errors'`,
    collect: (rows, metrics) => {
      const value = rows[0][0].value;
      log("Fetched number of kill connection errors/sec", value);
      metrics.mssql_kill_connection_errors.set(value);
    },
  },
];
