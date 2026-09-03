import createDebug from "debug";
import client from "prom-client";

import { loadConfig } from "./config.js";
import { createApp } from "./server.js";

const appLog = createDebug("app");

const config = loadConfig();

if (config.collectDefaultMetrics) {
  client.collectDefaultMetrics();
}

const app = createApp(config);

const server = app.listen(config.port, () => {
  appLog(
    "Prometheus-MSSQL Exporter listening on port %d, monitoring %s@%s:%d",
    config.port,
    config.connect.authentication.options.userName,
    config.connect.server,
    config.connect.options.port,
  );
});

const shutdown = (signal) => {
  appLog("Received %s, shutting down", signal);
  server.close(() => process.exit(0));
  // Force exit if connections do not drain promptly.
  setTimeout(() => process.exit(0), 5_000).unref();
};

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
