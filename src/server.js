import createDebug from "debug";
import express from "express";
import client from "prom-client";

import { entries } from "./metrics.js";
import * as defaultDb from "./db.js";

const appLog = createDebug("app");
const metricsLog = createDebug("metrics");

const sanitizeHeader = (value) => String(value).replace(/[\r\n]+/g, " ");

/**
 * Runs every collector sequentially over a single connection. Individual
 * query failures or empty result sets are logged and skipped, never fatal.
 *
 * @param {import("tedious").Connection} connection
 * @param {{ runQuery: typeof defaultDb.runQuery }} db
 */
async function collectAll(connection, db) {
  for (const [name, collector] of Object.entries(entries)) {
    try {
      const rows = await db.runQuery(connection, collector.query);
      if (rows.length === 0) {
        metricsLog("Query for metric %s returned 0 rows", name);
        continue;
      }
      collector.collect(rows, collector.metrics);
    } catch (error) {
      console.error(`Error collecting metric ${name}:`, error.message || error);
    }
  }
}

/**
 * Builds the Express application. The database layer is injectable so the
 * server can be unit tested without a real SQL Server.
 *
 * @param {ReturnType<import("./config.js").loadConfig>} config
 * @param {Partial<typeof defaultDb>} [deps]
 * @returns {import("express").Express}
 */
export function createApp(config, deps = {}) {
  const db = { ...defaultDb, ...deps };
  const app = express();

  app.get("/", (req, res) => res.redirect("/metrics"));

  app.get("/healthz", (req, res) => res.json({ status: "ok" }));

  app.get("/metrics", async (req, res) => {
    res.contentType(client.register.contentType);

    let connection;
    try {
      appLog("Received /metrics request");
      connection = await db.connect(config);
      await collectAll(connection, db);
      res.send(await client.register.metrics());
      appLog("Successfully processed /metrics request");
    } catch (error) {
      console.error("Failed to scrape SQL Server:", error.message || error);
      entries.mssql_up.metrics.mssql_up.set(0);
      res.setHeader("X-Error", sanitizeHeader(error.message || error));
      res.send(await client.register.getSingleMetricAsString("mssql_up"));
    } finally {
      if (connection) {
        connection.close();
      }
    }
  });

  return app;
}
