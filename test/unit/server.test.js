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

  it("keeps scraping when a single collector query fails", async () => {
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
    expect(connection.close).toHaveBeenCalledOnce();
  });
});
