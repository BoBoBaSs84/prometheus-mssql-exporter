import createDebug from "debug";

import { byName } from "./_util.js";

const log = createDebug("metrics");

/** counter_name → gauge name, for the perf-counter batch collectors below. */
const ACCESS_METHODS = {
  "Page Splits/sec": "mssql_page_splits_total",
  "Full Scans/sec": "mssql_full_scans_total",
  "Range Scans/sec": "mssql_range_scans_total",
  "Index Searches/sec": "mssql_index_searches_total",
  "Forwarded Records/sec": "mssql_forwarded_records_total",
};
const SQL_STATISTICS = {
  "SQL Compilations/sec": "mssql_sql_compilations_total",
  "SQL Re-Compilations/sec": "mssql_sql_recompilations_total",
};
const LOCK_STATS = {
  "Lock Waits/sec": "mssql_lock_waits_total",
  "Lock Wait Time (ms)": "mssql_lock_wait_time_ms_total",
  "Number of Lock Timeouts/sec": "mssql_lock_timeouts_total",
};
const LATCH_STATS = {
  "Latch Waits/sec": "mssql_latch_waits_total",
  "Total Latch Wait Time (ms)": "mssql_latch_wait_time_ms_total",
};

const counterCollector = (mapping) => (rows, metrics) => {
  const counters = byName(rows);
  for (const [counter, metric] of Object.entries(mapping)) {
    metrics[metric].set(counters.get(counter) ?? 0);
  }
};

const gaugesFor = (mapping, help) => Object.fromEntries(Object.values(mapping).map((name) => [name, { help }]));

const inClause = (mapping) =>
  Object.keys(mapping)
    .map((name) => `'${name}'`)
    .join(", ");

/** Throughput and contention performance counters (cumulative, use rate()). */
export default [
  {
    name: "mssql_deadlocks",
    gauges: {
      mssql_deadlocks_per_second: {
        name: "mssql_deadlocks",
        help: "Number of lock requests per second that resulted in a deadlock since last restart",
      },
    },
    query: `SELECT cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Number of Deadlocks/sec' AND instance_name = '_Total'`,
    collect: (rows, metrics) => {
      const value = rows[0][0].value;
      log("Fetched number of deadlocks/sec", value);
      metrics.mssql_deadlocks_per_second.set(value);
    },
  },
  {
    name: "mssql_batch_requests",
    gauges: {
      mssql_batch_requests: {
        help: "Number of Transact-SQL command batches received per second. This statistic is affected by all constraints (such as I/O, number of users, cachesize, complexity of requests, and so on). High batch requests mean good throughput",
      },
    },
    query: `SELECT TOP 1 cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Batch Requests/sec'`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const value = row[0].value;
        log("Fetched number of batch requests per second", value);
        metrics.mssql_batch_requests.set(value);
      }
    },
  },
  {
    name: "mssql_transactions",
    gauges: {
      mssql_transactions: {
        help: "Number of transactions started for the database per second. Transactions/sec does not count XTP-only transactions (transactions started by a natively compiled stored procedure.)",
        labelNames: ["database"],
      },
    },
    query: `SELECT rtrim(instance_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE counter_name = 'Transactions/sec' AND instance_name <> '_Total'`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const database = row[0].value;
        const value = row[1].value;
        log("Fetched number of transactions per second", database, value);
        metrics.mssql_transactions.set({ database }, value);
      }
    },
  },
  {
    name: "mssql_sql_statistics",
    gauges: gaugesFor(SQL_STATISTICS, "Cumulative count since last restart (use rate())"),
    query: `SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%SQL Statistics%' AND counter_name IN (${inClause(SQL_STATISTICS)})`,
    collect: counterCollector(SQL_STATISTICS),
  },
  {
    name: "mssql_access_methods",
    gauges: gaugesFor(ACCESS_METHODS, "Cumulative count since last restart (use rate())"),
    query: `SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Access Methods%' AND counter_name IN (${inClause(ACCESS_METHODS)})`,
    collect: counterCollector(ACCESS_METHODS),
  },
  {
    name: "mssql_lock_stats",
    gauges: gaugesFor(LOCK_STATS, "Cumulative lock statistic since last restart, all lock types (use rate())"),
    query: `SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Locks%' AND instance_name = '_Total' AND counter_name IN (${inClause(LOCK_STATS)})`,
    collect: counterCollector(LOCK_STATS),
  },
  {
    name: "mssql_latch_stats",
    gauges: gaugesFor(LATCH_STATS, "Cumulative latch statistic since last restart (use rate())"),
    query: `SELECT RTRIM(counter_name), cntr_value
FROM sys.dm_os_performance_counters
WHERE object_name LIKE '%Latches%' AND counter_name IN (${inClause(LATCH_STATS)})`,
    collect: counterCollector(LATCH_STATS),
  },
];
