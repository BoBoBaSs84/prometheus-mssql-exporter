import { performance } from "node:perf_hooks";

import createDebug from "debug";
import express from "express";
import client from "prom-client";

import { entries as defaultEntries, meta as defaultMeta, buildEntries, buildMeta } from "./metrics.js";
import * as defaultDb from "./db.js";

const appLog = createDebug("app");
const metricsLog = createDebug("metrics");

const sanitizeHeader = (value) => String(value).replace(/[\r\n]+/g, " ");

const TARGET_RE = /^[A-Za-z0-9._-]+(?::\d{1,5})?$/;

/**
 * Runs every collector sequentially over a single connection, recording per
 * collector timing/success on `meta`. A collector's query failure, timeout or
 * empty result set is logged and skipped, never fatal.
 *
 * @param {import("tedious").Connection} connection
 * @param {typeof defaultDb} db
 * @param {Record<string, { query: string, collect: Function, metrics: object }>} entries
 * @param {ReturnType<typeof buildMeta>} meta
 * @param {number} timeoutMs
 */
async function collectAll(connection, db, entries, meta, timeoutMs) {
  for (const [name, collector] of Object.entries(entries)) {
    const started = performance.now();
    let success = 1;
    try {
      const rows = await db.runQuery(connection, collector.query, timeoutMs);
      if (rows.length === 0) {
        metricsLog("Query for metric %s returned 0 rows", name);
      } else {
        collector.collect(rows, collector.metrics);
      }
    } catch (error) {
      success = 0;
      console.error(`Error collecting metric ${name}:`, error.message || error);
    } finally {
      meta.collectorDuration.set({ collector: name }, (performance.now() - started) / 1000);
      meta.collectorSuccess.set({ collector: name }, success);
    }
  }
}

/**
 * Connect, collect, close. Throws if the connection itself fails; individual
 * collector failures are swallowed by `collectAll`.
 */
async function scrape(db, config, entries, meta, timeoutMs) {
  const started = performance.now();
  let connection;
  try {
    connection = await db.connect(config);
    await collectAll(connection, db, entries, meta, timeoutMs);
  } finally {
    if (connection) {
      connection.close();
    }
    meta.scrapeDuration.set((performance.now() - started) / 1000);
  }
}

/** Derive a per-target connection config from the exporter's base config. */
function buildProbeConfig(base, target) {
  const [host, portString] = target.split(":");
  return {
    ...base,
    connect: {
      ...base.connect,
      server: host,
      options: {
        ...base.connect.options,
        port: portString ? Number(portString) : base.connect.options.port,
      },
    },
  };
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
  const timeoutMs = config.queryTimeoutMs ?? 0;
  const app = express();

  app.get("/", (req, res) => res.redirect("/metrics"));

  app.get("/healthz", (req, res) => res.json({ status: "ok" }));

  app.get("/metrics", async (req, res) => {
    res.contentType(client.register.contentType);
    try {
      appLog("Received /metrics request");
      await scrape(db, config, defaultEntries, defaultMeta, timeoutMs);
      res.send(await client.register.metrics());
      appLog("Successfully processed /metrics request");
    } catch (error) {
      console.error("Failed to scrape SQL Server:", error.message || error);
      defaultEntries.mssql_up.metrics.mssql_up.set(0);
      res.setHeader("X-Error", sanitizeHeader(error.message || error));
      res.send(await client.register.getSingleMetricAsString("mssql_up"));
    }
  });

  if (config.probeEnabled !== false) {
    app.get("/probe", async (req, res) => {
      const target = String(req.query.target || "");
      if (!TARGET_RE.test(target)) {
        res.status(400).type("text/plain").send("missing or invalid ?target=host[:port]");
        return;
      }

      const registry = new client.Registry();
      const entries = buildEntries(registry);
      const meta = buildMeta(registry);
      res.contentType(registry.contentType);

      try {
        appLog("Received /probe request for %s", target);
        await scrape(db, buildProbeConfig(config, target), entries, meta, timeoutMs);
      } catch (error) {
        console.error(`Failed to probe ${target}:`, error.message || error);
        entries.mssql_up.metrics.mssql_up.set(0);
        res.setHeader("X-Error", sanitizeHeader(error.message || error));
      }
      res.send(await registry.metrics());
    });
  }

  return app;
}
