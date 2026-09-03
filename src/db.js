import createDebug from "debug";
import { Connection, Request } from "tedious";

const dbLog = createDebug("db");

/**
 * Opens a connection to the configured SQL Server instance.
 *
 * @param {ReturnType<import("./config.js").loadConfig>} config
 * @returns {Promise<import("tedious").Connection>}
 */
export function connect(config) {
  return new Promise((resolve, reject) => {
    const { server, options, authentication } = config.connect;
    dbLog(
      "Connecting to %s@%s:%d (encrypt: %s, trustServerCertificate: %s)",
      authentication.options.userName,
      server,
      options.port,
      options.encrypt,
      options.trustServerCertificate,
    );

    const connection = new Connection(config.connect);

    connection.on("error", (error) => {
      dbLog("Connection error: %s", error.message || error);
    });
    connection.on("end", () => {
      dbLog("Connection closed");
    });

    connection.connect((error) => {
      if (error) {
        reject(error);
      } else {
        dbLog("Connected");
        resolve(connection);
      }
    });
  });
}

const INTEGER_STRING = /^-?\d+$/;

/**
 * tedious returns `bigint` columns as decimal strings (and `BigInt` for some
 * types), which prom-client's `Gauge.set()` rejects. Coerce those to plain
 * numbers. Non-integer strings (database names, version strings, file paths)
 * are left untouched.
 */
export const coerceRow = (row) => {
  for (const cell of row) {
    if (typeof cell.value === "bigint") {
      cell.value = Number(cell.value);
    } else if (typeof cell.value === "string" && INTEGER_STRING.test(cell.value)) {
      cell.value = Number(cell.value);
    }
  }
  return row;
};

/**
 * Runs a single SQL statement and resolves with the tedious row array
 * (row shape: `rows[i][columnIndex].value`).
 *
 * @param {import("tedious").Connection} connection
 * @param {string} sql
 * @param {number} [timeoutMs] per-request timeout; 0 disables it
 * @returns {Promise<Array>}
 */
export function runQuery(connection, sql, timeoutMs = 0) {
  return new Promise((resolve, reject) => {
    const request = new Request(sql, (error, _rowCount, rows) => {
      if (error) {
        reject(error);
      } else {
        resolve((rows || []).map(coerceRow));
      }
    });
    if (timeoutMs > 0) {
      request.setTimeout(timeoutMs);
    }
    connection.execSql(request);
  });
}
