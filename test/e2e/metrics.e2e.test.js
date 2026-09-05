import { execFile } from "node:child_process";
import { promisify } from "node:util";

import { describe, it, expect, beforeAll, afterAll } from "vitest";
import request from "supertest";
import { MSSQLServerContainer } from "@testcontainers/mssqlserver";

import { createApp } from "../../src/server.js";
import { loadConfig } from "../../src/config.js";
import { collectors, meta } from "../../src/metrics.js";
import { connect, runQuery } from "../../src/db.js";
import { runScript, waitForAgent, runJobOnce, retry } from "./helpers/provision.js";

const IMAGE = process.env.MSSQL_IMAGE || "mcr.microsoft.com/mssql/server:2022-latest";
const PASSWORD = "Str0ng_Passw0rd!";

/** Least-privilege login provisioned from `sql/exporter-permissions.sql`. */
const EXPORTER_LOGIN = "exporter";
/** Same grants, but without EXECUTE on msdb.dbo.agent_datetime. */
const NOEXEC_LOGIN = "exporter_noexec";
const EXPORTER_PASSWORD = "Str0ng_Exp0rter!";
const JOB = "exporter_e2e_job";

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

const sampleLines = (body) => body.split("\n").filter((line) => line && !line.startsWith("#"));

/** Samples as `{ 'mssql_up': 1, 'mssql_database_state{database="master"}': 0, ... }`. */
const asMap = (body) =>
  Object.fromEntries(
    sampleLines(body)
      .map((line) => line.split(/ (?=[^ ]*$)/))
      .map(([key, value]) => [key, Number(value)]),
  );

/** Per-collector outcome keyed by collector name: `{ mssql_agent_jobs: 1, ... }`. */
const collectorSuccess = (body) =>
  Object.fromEntries(
    sampleLines(body)
      .filter((line) => line.startsWith("mssql_collector_success{"))
      .map((line) => {
        const [key, value] = line.split(/ (?=[^ ]*$)/);
        return [key.match(/collector="([^"]+)"/)[1], Number(value)];
      }),
  );

describe.skipIf(!containerRuntimeAvailable)(`E2E against ${IMAGE}`, () => {
  let container;
  let admin;
  let app;

  const envFor = (userName, password) => ({
    SERVER: container.getHost(),
    PORT: String(container.getPort()),
    USERNAME: userName,
    PASSWORD: password,
    ENCRYPT: "false",
    TRUST_SERVER_CERTIFICATE: "true",
    COLLECT_DEFAULT_METRICS: "false",
  });

  const buildApp = (userName, password) => createApp(loadConfig(envFor(userName, password)));

  beforeAll(async () => {
    container = await new MSSQLServerContainer(IMAGE)
      .acceptLicense()
      .withPassword(PASSWORD)
      // Equivalent to `mssql-conf set sqlagent.enabled true`. Without it the
      // Agent collectors have nothing to report and the msdb grants the README
      // documents are never exercised.
      .withEnvironment({ MSSQL_AGENT_ENABLED: "true" })
      .start();

    app = buildApp(container.getUsername(), container.getPassword());

    // Provision the documented least-privilege logins and an Agent job as sa.
    admin = await connect(loadConfig(envFor(container.getUsername(), container.getPassword())));

    for (const login of [EXPORTER_LOGIN, NOEXEC_LOGIN]) {
      await runScript(admin, "exporter-permissions.sql", { __LOGIN__: login, __PASSWORD__: EXPORTER_PASSWORD });
    }
    // The negative case: everything the README grants except this one.
    await runQuery(admin, "USE msdb");
    await runQuery(admin, `REVOKE EXECUTE ON [dbo].[agent_datetime] TO [${NOEXEC_LOGIN}]`);

    await waitForAgent(admin);
    await retry("the msdb job procedures to accept writes", () => runScript(admin, "agent-job.sql", { __JOB__: JOB }));
    await runJobOnce(admin, JOB);
  });

  afterAll(async () => {
    admin?.close();
    await container?.stop();
  });

  const missingRequired = (body) => [...requiredNames].filter((name) => !scrapedNames(body).has(name));

  /** Scrape until every required family is present (some DMVs lag just after boot). */
  const scrapeWhenReady = async (target = app) => {
    let res;
    for (let attempt = 0; attempt < 10; attempt++) {
      res = await request(target).get("/metrics");
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

    const metrics = asMap(res.text);

    expect(metrics.mssql_up).toBe(1);
    expect(metrics.mssql_product_version).toBeGreaterThanOrEqual(15);
    expect(metrics.mssql_instance_local_time).toBeGreaterThan(0);
    expect(metrics.mssql_total_physical_memory_kb).toBeGreaterThan(0);
    expect(metrics.mssql_scrape_duration_seconds).toBeGreaterThan(0);
    expect(metrics['mssql_database_state{database="master"}']).toBe(0);

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

  it("reports the SQL Agent job the fixture created", async () => {
    const metrics = asMap((await scrapeWhenReady()).text);

    expect(metrics.mssql_agent_up).toBe(1);
    expect(metrics[`mssql_agent_job_enabled{job="${JOB}"}`]).toBe(1);
    expect(metrics[`mssql_agent_job_last_run_success{job="${JOB}"}`]).toBe(1);
    // Non-zero only if msdb.dbo.agent_datetime() actually executed.
    expect(metrics[`mssql_agent_job_last_run_timestamp{job="${JOB}"}`]).toBeGreaterThan(0);
  });

  it("collects everything with only the permissions the README documents", async () => {
    const asSa = collectorSuccess((await scrapeWhenReady()).text);
    const asExporter = collectorSuccess((await scrapeWhenReady(buildApp(EXPORTER_LOGIN, EXPORTER_PASSWORD))).text);

    // The least-privilege login must not lose a single collector against sa.
    expect(asExporter).toEqual(asSa);
    expect(asExporter.mssql_agent_up).toBe(1);
    expect(asExporter.mssql_agent_jobs).toBe(1);
  });

  it("fails the agent job collector without EXECUTE on msdb.dbo.agent_datetime", async () => {
    // /probe builds a throwaway registry, so this failing scrape cannot leak
    // into the default-registry assertions above.
    const res = await request(buildApp(NOEXEC_LOGIN, EXPORTER_PASSWORD)).get(`/probe?target=${container.getHost()}:${container.getPort()}`);
    expect(res.status).toBe(200);

    const success = collectorSuccess(res.text);
    expect(success.mssql_agent_jobs).toBe(0);
    // ... and only that collector: the rest of the grants are still in place.
    expect(success.mssql_agent_up).toBe(1);
    expect(success.mssql_backups).toBe(1);
    expect(success.mssql_suspect_pages).toBe(1);
  });
});
