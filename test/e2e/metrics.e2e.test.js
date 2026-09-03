import { execFile } from "node:child_process";
import { promisify } from "node:util";

import { describe, it, expect, beforeAll, afterAll } from "vitest";
import request from "supertest";
import { MSSQLServerContainer } from "@testcontainers/mssqlserver";

import { createApp } from "../../src/server.js";
import { loadConfig } from "../../src/config.js";
import { entries } from "../../src/metrics.js";

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

/** All metric family names declared by the exporter. */
const declaredNames = new Set(Object.values(entries).flatMap((entry) => Object.values(entry.metrics).map((metric) => metric.name)));

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

  /** Scrape until every declared family is present (some DMVs lag just after boot). */
  const scrapeWhenReady = async () => {
    let res;
    for (let attempt = 0; attempt < 10; attempt++) {
      res = await request(app).get("/metrics");
      if (res.status === 200 && scrapedNames(res.text).size === declaredNames.size) {
        return res;
      }
      await new Promise((resolve) => setTimeout(resolve, 2_000));
    }
    return res;
  };

  it("scrapes a real instance and exposes every declared metric family", async () => {
    const res = await scrapeWhenReady();
    expect(res.status).toBe(200);

    const lines = res.text.split("\n").filter((line) => line && !line.startsWith("#"));
    const asMap = Object.fromEntries(lines.map((line) => line.split(/ (?=[^ ]*$)/)).map(([key, value]) => [key, Number(value)]));

    expect(asMap.mssql_up).toBe(1);
    expect(asMap.mssql_product_version).toBeGreaterThanOrEqual(15);
    expect(asMap.mssql_instance_local_time).toBeGreaterThan(0);
    expect(asMap.mssql_total_physical_memory_kb).toBeGreaterThan(0);
    expect(asMap['mssql_database_state{database="master"}']).toBe(0);

    expect(scrapedNames(res.text)).toEqual(declaredNames);
  });
});
