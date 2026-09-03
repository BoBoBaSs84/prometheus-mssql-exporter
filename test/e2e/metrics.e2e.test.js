import { execFile } from "node:child_process";
import { promisify } from "node:util";

import { describe, it, expect, beforeAll, afterAll } from "vitest";
import request from "supertest";
import { MSSQLServerContainer } from "@testcontainers/mssqlserver";

import { createApp } from "../../src/server.js";
import { loadConfig } from "../../src/config.js";
import { collectors, meta } from "../../src/metrics.js";

const IMAGE = process.env.MSSQL_IMAGE || "mcr.microsoft.com/mssql/server:2022-latest";
const PASSWORD = "Str0ng_Passw0rd!";

const execFileAsync = promisify(execFile);
const canRun = (bin) =>
  execFileAsync(bin, ["info"])
    .then(() => true)
    .catch(() => false);

// Testcontainers supports Docker and Podman; skip the suite only when neither
// is reachable (e.g. a CI runner without a container runtime).
const containerRuntimeAvailable = Boolean(process.env.DOCKER_HOST) || (await canRun("docker")) || (await canRun("podman"));

const gaugeNames = (collector) => Object.entries(collector.gauges).map(([key, spec]) => spec.name ?? key);
const metaNames = Object.values(meta).map((gauge) => gauge.name);

/** Every metric family the exporter declares (collectors + scrape-quality gauges). */
const declaredNames = new Set([...collectors.flatMap(gaugeNames), ...metaNames]);

/** Families that may legitimately be absent on a vanilla instance (Always On off, no Agent jobs, ...). */
const optionalNames = new Set(collectors.filter((c) => c.optional).flatMap(gaugeNames));

/** Families that must always appear. */
const requiredNames = new Set([...declaredNames].filter((name) => !optionalNames.has(name)));

/** Metric family names that actually produced a sample in the scrape. */
const scrapedNames = (body) =>
  new Set(
    body
      .split("\n")
      .filter((line) => line && !line.startsWith("#"))
      .map((line) => line.split(/[ {]/, 1)[0])
      .filter((name) => name.startsWith("mssql_")),
  );

describe.skipIf(!containerRuntimeAvailable)(`E2E against ${IMAGE}`, () => {
  let container;
  let app;

  beforeAll(async () => {
    container = await new MSSQLServerContainer(IMAGE).acceptLicense().withPassword(PASSWORD).start();

    app = createApp(
      loadConfig({
        SERVER: container.getHost(),
        PORT: String(container.getPort()),
        USERNAME: container.getUsername(),
        PASSWORD: container.getPassword(),
        ENCRYPT: "false",
        TRUST_SERVER_CERTIFICATE: "true",
        COLLECT_DEFAULT_METRICS: "false",
      }),
    );
  });

  afterAll(async () => {
    await container?.stop();
  });

  const missingRequired = (body) => [...requiredNames].filter((name) => !scrapedNames(body).has(name));

  /** Scrape until every required family is present (some DMVs lag just after boot). */
  const scrapeWhenReady = async () => {
    let res;
    for (let attempt = 0; attempt < 10; attempt++) {
      res = await request(app).get("/metrics");
      if (res.status === 200 && missingRequired(res.text).length === 0) {
        return res;
      }
      await new Promise((resolve) => setTimeout(resolve, 2_000));
    }
    return res;
  };

  it("scrapes a real instance and exposes every required metric family", async () => {
    const res = await scrapeWhenReady();
    expect(res.status).toBe(200);

    const lines = res.text.split("\n").filter((line) => line && !line.startsWith("#"));
    const asMap = Object.fromEntries(lines.map((line) => line.split(/ (?=[^ ]*$)/)).map(([key, value]) => [key, Number(value)]));

    expect(asMap.mssql_up).toBe(1);
    expect(asMap.mssql_product_version).toBeGreaterThanOrEqual(15);
    expect(asMap.mssql_instance_local_time).toBeGreaterThan(0);
    expect(asMap.mssql_total_physical_memory_kb).toBeGreaterThan(0);
    expect(asMap.mssql_scrape_duration_seconds).toBeGreaterThan(0);
    expect(asMap['mssql_database_state{database="master"}']).toBe(0);

    // every required family present ...
    expect(missingRequired(res.text)).toEqual([]);
    // ... and nothing outside what the exporter declares
    expect([...scrapedNames(res.text)].filter((name) => !declaredNames.has(name))).toEqual([]);
  });

  it("serves an isolated registry per /probe target", async () => {
    const res = await request(app).get(`/probe?target=${container.getHost()}:${container.getPort()}`);
    expect(res.status).toBe(200);
    const up = res.text.split("\n").find((l) => l.startsWith("mssql_up "));
    expect(up).toBe("mssql_up 1");
    // probe output must not carry the default-registry process metrics
    expect(res.text).not.toMatch(/^process_/m);
  });
});
