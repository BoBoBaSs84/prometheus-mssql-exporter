import instance from "./instance.js";
import connections from "./connections.js";
import errors from "./errors.js";
import databases from "./databases.js";
import io from "./io.js";
import throughput from "./throughput.js";
import memory from "./memory.js";
import activity from "./activity.js";
import waits from "./waits.js";
import space from "./space.js";
import settings from "./settings.js";
import backups from "./backups.js";
import availability from "./availability.js";
import alwayson from "./alwayson.js";
import agent from "./agent.js";

/**
 * The ordered list of collectors. Each collector is a plain object:
 *
 *   {
 *     name: "mssql_<group>",              // unique key, also the entries[] key
 *     optional: false,                    // may legitimately return 0 rows on a vanilla instance
 *     gauges: { <key>: { name?, help, labelNames? } },
 *     query: "<single SQL statement>",
 *     collect: (rows, metrics) => { ... },
 *   }
 */
export const collectors = [
  ...instance,
  ...connections,
  ...errors,
  ...databases,
  ...io,
  ...throughput,
  ...memory,
  ...activity,
  ...waits,
  ...space,
  ...settings,
  ...backups,
  ...availability,
  ...alwayson,
  ...agent,
];
