# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

A Prometheus exporter for Microsoft SQL Server. A single Node.js process (ES modules; Express 5 + `tedious` + `prom-client` 15) that exposes `/metrics`. On every scrape it opens a fresh DB connection, runs a fixed set of SQL queries, maps the results onto `prom-client` gauges, closes the connection, and returns the registry. Distributed as a multi-arch Docker image (`awaragi/prometheus-mssql-exporter`). Node >= 20.12.

## Commands

```shell
npm ci

# Unit tests — no setup, no Docker (default `npm test`)
npm run test:unit
npm run test:watch
npm run coverage

# E2E — boots a real SQL Server via Testcontainers; needs Docker; auto-skips without it
npm run test:e2e
MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2019-latest npm run test:e2e

# Lint / format (ESLint flat config + Prettier, printWidth 160)
npm run lint
npm run format
npm run format:check

# Run a local SQL Server to develop against (needs $MSSQL_SA_PASSWORD)
npm run test:mssql:2022      # also :2019, :2025

# Run the exporter (loads .env via node --env-file-if-exists; copy .env.example first)
npm start
npm run dev                  # + --watch

# Print every query and its metric help text (for DBA review)
npm run metrics

# Docker
npm run docker:build
```

Run one unit test file / name filter: `npx vitest run --project unit test/unit/config.test.js -t "applies defaults"`.

### Debug channels

`DEBUG` is a comma-delimited list of `app`, `db`, `metrics`, `queries` (the `debug` package).

## Architecture

### Collectors — `src/collectors/*.js` + `src/metrics.js`

Each `src/collectors/<domain>.js` default-exports an array of **declarative** collectors:

```js
{
  name: "mssql_<group>",          // unique key, also the entries[] key and metrics-docs heading
  optional: false,                // true = may return 0 rows on a vanilla instance (Always On, Agent)
  gauges: { <key>: { name?, help, labelNames? } },   // name defaults to key
  query: "<single SQL statement>",
  collect: (rows, metrics) => { /* rows[i][col].value (positional); metrics.<key>.set(...) */ },
}
```

`src/collectors/index.js` concatenates them into `collectors` (order = output order).
`src/metrics.js` binds them to a prom-client registry:

- `buildEntries(registry)` → `{ [name]: { ...collector, metrics: <live gauge map> } }`
- `entries` / `meta` — bound to the default registry (`/metrics` + tests)
- `buildMeta(registry)` — the `mssql_scrape_*` / `mssql_collector_*` gauges

**To add a metric:** add a collector to the right domain file, add a fixture row in
`test/fixtures/rows.js` (a unit test asserts fixtures ↔ collectors parity), add ≥1 value
assertion in `test/unit/collectors.test.js`, and if it can return 0 rows on a fresh instance
mark it `optional`. `prom-client` 15 rejects non-numeric `gauge.set()` — pass real numbers
(`src/db.js` `coerceRow` already converts `bigint`/integer-string columns).

### Module layout

- **`src/config.js`** — `loadConfig(env)` → validated config; throws listing missing required vars (`SERVER`, `USERNAME`, `PASSWORD`). Also `queryTimeoutMs` (`QUERY_TIMEOUT_MS`, 30000), `connectTimeout` (`CONNECT_TIMEOUT_MS`, 15000), `probeEnabled` (`PROBE_ENABLED`, true).
- **`src/db.js`** — `connect(config)` (tedious, callback form) and `runQuery(connection, sql, timeoutMs)` → rows (`request.setTimeout` per collector). `coerceRow` normalises `bigint`/integer-string columns to numbers. All DB logging.
- **`src/server.js`** — `createApp(config, deps)` → Express app, no `listen`. `deps` injects `{ connect, runQuery }`. `collectAll` runs collectors sequentially, timing each and setting `meta.collector*`; per-collector failures/empty results are logged and skipped. `/metrics` → default registry (+ default process metrics). `/probe?target=host[:port]` → fresh `client.Registry()` per request via `buildEntries`, target host/port overridden, isolated from concurrent probes; 400 on bad target; gated by `config.probeEnabled`. `/` → 302 `/metrics`. `/healthz` → `{status:"ok"}`, no DB.
- **`src/index.js`** — thin entrypoint: `loadConfig` → optional `collectDefaultMetrics()` → `createApp` → `listen`; `SIGINT`/`SIGTERM` graceful shutdown.
- **`src/utils.js`** — `productVersionParse` parses the 4-part `SERVERPROPERTY('productversion')`.
- **`src/metrics-docs.js`** — `npm run metrics` iterates `collectors`, prints each query + gauges.

## Tests

- **Unit** (`test/unit/**`, Vitest project `unit`): collectors against `test/fixtures/rows.js`, `config`, `db` (`coerceRow`), and `server` with a stubbed `db` dep (`/metrics`, `/probe`, timeout wiring). This is `npm test`.
- **E2E** (`test/e2e/**`, project `e2e`): `@testcontainers/mssqlserver` boots SQL Server, `createApp` runs in-process, supertest scrapes `/metrics`. Asserts `(declared − optional) ⊆ scraped ⊆ declared` on metric **family names** (version-robust) plus a `/probe` isolation check. `describe.skipIf` skips when neither `docker` nor `podman` is reachable.
- Podman e2e: `DOCKER_HOST=npipe:////./pipe/podman-machine-default`, `TESTCONTAINERS_RYUK_DISABLED=true`.

CI (`.github/workflows/node.js.yml`): `lint-unit` job on Node 20/22/24; `e2e` job matrixed over MSSQL 2019/2022/2025 (2025 is `continue-on-error` while it may be preview-only).

## Release / publish

`.github/workflows/publish.yml` on a published GitHub Release: `docker/metadata-action` derives tags, `build-push-action` builds `linux/amd64,linux/arm64` and pushes to Docker Hub, then syncs the repo description from `README.md`.

## Conventions

- Dependency versions are pinned exactly (no `^`/`~`) in `package.json`.
- ES modules everywhere (`"type": "module"`); relative imports include the `.js` extension.
- Dockerfile is multi-stage, runs as non-root `node`, `COPY src ./src`, `CMD ["node", "src/index.js"]`.
- Required SQL Server grants for the exporter's login: `VIEW ANY DEFINITION`, `VIEW SERVER STATE`.
