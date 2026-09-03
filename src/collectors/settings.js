import createDebug from "debug";

const log = createDebug("metrics");

const TRACKED = [
  "max server memory (MB)",
  "min server memory (MB)",
  "max degree of parallelism",
  "cost threshold for parallelism",
  "max worker threads",
  "optimize for ad hoc workloads",
  "backup compression default",
  "fill factor (%)",
  "priority boost",
  "lightweight pooling",
];

/** A curated subset of sys.configurations, for config-drift alerting. */
export default [
  {
    name: "mssql_configuration",
    gauges: {
      mssql_configuration: { help: "value_in_use for a tracked sp_configure setting", labelNames: ["name"] },
    },
    query: `SELECT RTRIM(name), CAST(value_in_use AS FLOAT)
FROM sys.configurations
WHERE name IN (${TRACKED.map((n) => `'${n}'`).join(", ")})`,
    collect: (rows, metrics) => {
      log("Fetched configurations", rows.length);
      for (const row of rows) {
        metrics.mssql_configuration.set({ name: row[0].value }, row[1].value);
      }
    },
  },
];
