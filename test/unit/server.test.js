import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import request from "supertest";

import { createApp } from "../../src/server.js";

beforeEach(() => vi.spyOn(console, "error").mockImplementation(() => {}));
afterEach(() => vi.restoreAllMocks());

const config = {
  connect: {
    server: "db",
    authentication: { type: "default", options: { userName: "sa", password: "pw" } },
    options: { port: 1433 },
  },
  port: 4000,
};

const okConnection = () => ({ close: vi.fn() });

describe("server", () => {
  it("redirects / to /metrics", async () => {
    const app = createApp(config, { connect: async () => okConnection(), runQuery: async () => [] });
    const res = await request(app).get("/").redirects(0);
    expect(res.status).toBe(302);
    expect(res.headers.location).toBe("/metrics");
  });

  it("answers /healthz without touching the database", async () => {
    const connect = vi.fn();
    const app = createApp(config, { connect, runQuery: vi.fn() });
    const res = await request(app).get("/healthz");
    expect(res.status).toBe(200);
    expect(res.body).toEqual({ status: "ok" });
    expect(connect).not.toHaveBeenCalled();
  });

  it("serves metrics and closes the connection on success", async () => {
    const connection = okConnection();
    const app = createApp(config, {
      connect: async () => connection,
      runQuery: async (_connection, sql) => (sql === "SELECT 1" ? [[{ value: 1 }]] : []),
    });

    const res = await request(app).get("/metrics");

    expect(res.status).toBe(200);
    expect(res.headers["content-type"]).toMatch(/text\/plain/);
    expect(res.text).toMatch(/^mssql_up 1$/m);
    expect(connection.close).toHaveBeenCalledOnce();
  });

  it("reports mssql_up 0 with a sanitized X-Error header when the connection fails", async () => {
    const app = createApp(config, {
      connect: async () => {
        throw new Error("connect refused\nsecond line");
      },
      runQuery: async () => [],
    });

    const res = await request(app).get("/metrics");

    expect(res.status).toBe(200);
    expect(res.headers["x-error"]).toBe("connect refused second line");
    expect(res.text).toMatch(/^mssql_up 0$/m);
  });

  it("keeps scraping when a single collector query fails and records collector success", async () => {
    const connection = okConnection();
    const app = createApp(config, {
      connect: async () => connection,
      runQuery: async (_connection, sql) => {
        if (sql === "SELECT 1") return [[{ value: 1 }]];
        throw new Error("permission denied");
      },
    });

    const res = await request(app).get("/metrics");

    expect(res.status).toBe(200);
    expect(res.text).toMatch(/^mssql_up 1$/m);
    expect(res.text).toMatch(/^mssql_scrape_duration_seconds /m);
    expect(res.text).toMatch(/^mssql_collector_success\{collector="mssql_up"\} 1$/m);
    expect(res.text).toMatch(/^mssql_collector_success\{collector="mssql_io_stall"\} 0$/m);
    expect(connection.close).toHaveBeenCalledOnce();
  });

  it("passes the configured query timeout to runQuery", async () => {
    const runQuery = vi.fn(async () => []);
    const app = createApp({ ...config, queryTimeoutMs: 1234 }, { connect: async () => okConnection(), runQuery });

    await request(app).get("/metrics");

    expect(runQuery).toHaveBeenCalled();
    expect(runQuery.mock.calls.every(([, , timeout]) => timeout === 1234)).toBe(true);
  });

  describe("/probe", () => {
    it("400s without a valid target", async () => {
      const app = createApp(config, { connect: vi.fn(), runQuery: vi.fn() });
      expect((await request(app).get("/probe")).status).toBe(400);
      expect((await request(app).get("/probe?target=bad target!")).status).toBe(400);
    });

    it("connects to the requested target and returns an isolated registry", async () => {
      const seen = [];
      const app = createApp(config, {
        connect: async (cfg) => {
          seen.push([cfg.connect.server, cfg.connect.options.port]);
          return okConnection();
        },
        runQuery: async (_c, sql) => (sql === "SELECT 1" ? [[{ value: 1 }]] : []),
      });

      const res = await request(app).get("/probe?target=other-host:1444");

      expect(res.status).toBe(200);
      expect(seen).toContainEqual(["other-host", 1444]);
      expect(res.text).toMatch(/^mssql_up 1$/m);
      expect(res.text).not.toMatch(/^process_/m);
    });

    it("can be disabled", async () => {
      const app = createApp({ ...config, probeEnabled: false }, { connect: vi.fn(), runQuery: vi.fn() });
      expect((await request(app).get("/probe?target=host")).status).toBe(404);
    });
  });
});
