import createDebug from "debug";

import { productVersionParse } from "../utils.js";

const log = createDebug("metrics");

/** Instance liveness, version and clock. */
export default [
  {
    name: "mssql_up",
    gauges: {
      mssql_up: { help: "UP status" },
    },
    query: "SELECT 1",
    collect: (rows, metrics) => {
      const up = rows[0][0].value;
      log("Fetched status of instance", up);
      metrics.mssql_up.set(up);
    },
  },
  {
    name: "mssql_product_version",
    gauges: {
      mssql_product_version: { help: "Instance version (Major.Minor)" },
    },
    query: `SELECT CONVERT(VARCHAR(128), SERVERPROPERTY('productversion')) AS [ProductVersion];`,
    collect: (rows, metrics) => {
      const v = productVersionParse(rows[0][0].value);
      const version = Number(`${v.major}.${v.minor}`);
      log("Fetched version of instance", version);
      metrics.mssql_product_version.set(version);
    },
  },
  {
    name: "mssql_instance_local_time",
    gauges: {
      mssql_instance_local_time: { help: "Number of seconds since epoch on local instance" },
    },
    query: `SELECT DATEDIFF(SECOND, '19700101', GETUTCDATE()) AS [instance_local_time];`,
    collect: (rows, metrics) => {
      const localTime = rows[0][0].value;
      log("Fetched current time", localTime);
      metrics.mssql_instance_local_time.set(localTime);
    },
  },
];
