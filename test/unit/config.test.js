import { describe, it, expect } from "vitest";

import { loadConfig } from "../../src/config.js";

const base = { SERVER: "db", USERNAME: "sa", PASSWORD: "pw" };

describe("loadConfig", () => {
  it("lists every missing required variable", () => {
    expect(() => loadConfig({})).toThrow(/SERVER, USERNAME, PASSWORD/);
  });

  it("reports a single missing variable", () => {
    expect(() => loadConfig({ SERVER: "db", USERNAME: "sa" })).toThrow(/PASSWORD/);
  });

  it("applies defaults", () => {
    const config = loadConfig(base);
    expect(config.connect.options.port).toBe(1433);
    expect(config.connect.options.encrypt).toBe(true);
    expect(config.connect.options.trustServerCertificate).toBe(true);
    expect(config.connect.options.rowCollectionOnRequestCompletion).toBe(true);
    expect(config.port).toBe(4000);
    expect(config.collectDefaultMetrics).toBe(true);
  });

  it("parses overrides", () => {
    const config = loadConfig({
      ...base,
      PORT: "1444",
      EXPOSE: "9000",
      ENCRYPT: "false",
      TRUST_SERVER_CERTIFICATE: "false",
      COLLECT_DEFAULT_METRICS: "false",
    });
    expect(config.connect.options.port).toBe(1444);
    expect(config.port).toBe(9000);
    expect(config.connect.options.encrypt).toBe(false);
    expect(config.connect.options.trustServerCertificate).toBe(false);
    expect(config.collectDefaultMetrics).toBe(false);
  });

  it("falls back to the default when a numeric variable is not a number", () => {
    expect(loadConfig({ ...base, PORT: "nope" }).connect.options.port).toBe(1433);
  });

  it("carries authentication credentials through", () => {
    const { authentication } = loadConfig(base).connect;
    expect(authentication).toEqual({ type: "default", options: { userName: "sa", password: "pw" } });
  });
});
