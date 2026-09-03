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

### `src/metrics.js` — the collectors

Exports `entries`, an ordered object mapping a collector name to:

```js
{
  metrics: { <prom_metric_name>: new client.Gauge({ name, help, labelNames }) },
  query:   "<single SQL statement>",
  collect: (rows, metrics) => { /* read tedious rows, call metrics.<name>.set(...) */ },
}
```

- `rows` is the `tedious` row-array form: `rows[i][columnIndex].value` (columns are positional).
- One collector may own several gauges (e.g. `mssql_buffer_manager`, `mssql_os_sys_memory`).
- **To add a metric:** add a collector to `entries` — `server.js` iterates it generically. Then add a fixture row in `test/fixtures/rows.js` (a unit test asserts fixtures and `entries` stay in sync) and, ideally, a value assertion in `test/unit/collectors.test.js`.
- `prom-client` 15 rejects non-numeric `gauge.set()` values — collectors must pass real numbers.

### Module layout

- **`src/config.js`** — `loadConfig(env)` → validated config; throws listing every missing required var (`SERVER`, `USERNAME`, `PASSWORD`). Defaults: port 1433, encrypt/trustServerCertificate true, expose 4000, collectDefaultMetrics true.
- **`src/db.js`** — `connect(config)` (tedious, callback form) and `runQuery(connection, sql)` → rows. `runQuery` coerces `bigint` columns (tedious returns them as strings) to numbers, otherwise `prom-client` 15 rejects them. All DB logging.
- **`src/server.js`** — `createApp(config, deps)` → Express app, no `listen`. `deps` injects `{ connect, runQuery }` so unit tests never hit a real DB. `collectAll` runs collectors sequentially; per-collector query errors and empty result sets are logged and skipped, never fatal. `/metrics` success → registry; failure → `mssql_up 0` + sanitized `X-Error` header. `/` → 302 `/metrics`. `/healthz` → `{status:"ok"}`, no DB.
- **`src/index.js`** — thin entrypoint: `loadConfig` → optional `collectDefaultMetrics()` → `createApp` → `listen`; `SIGINT`/`SIGTERM` graceful shutdown.
- **`src/utils.js`** — `productVersionParse` parses the 4-part `SERVERPROPERTY('productversion')`; `mssql_product_version` is emitted as the number `Major.Minor`.
- **`src/metrics-docs.js`** — `npm run metrics` prints each query + help. Documentation only.

## Tests

- **Unit** (`test/unit/**`, Vitest project `unit`): collectors against `test/fixtures/rows.js`, `config`, and `server` with a stubbed `db` dep. This is `npm test`.
- **E2E** (`test/e2e/**`, project `e2e`): `@testcontainers/mssqlserver` boots SQL Server, `createApp` runs in-process, supertest scrapes `/metrics`, and the test asserts the set of metric **family names** in the output equals the set declared by `entries` (version-robust — does not enumerate every labeled series). `describe.skipIf` skips the whole suite when `docker info` fails.

CI (`.github/workflows/node.js.yml`): `lint-unit` job on Node 20/22/24; `e2e` job matrixed over MSSQL 2019/2022/2025 (2025 is `continue-on-error` while it may be preview-only).

## Release / publish

`.github/workflows/publish.yml` on a published GitHub Release: `docker/metadata-action` derives tags, `build-push-action` builds `linux/amd64,linux/arm64` and pushes to Docker Hub, then syncs the repo description from `README.md`.

## Conventions

- Dependency versions are pinned exactly (no `^`/`~`) in `package.json`.
- ES modules everywhere (`"type": "module"`); relative imports include the `.js` extension.
- Dockerfile is multi-stage, runs as non-root `node`, `COPY src ./src`, `CMD ["node", "src/index.js"]`.
- Required SQL Server grants for the exporter's login: `VIEW ANY DEFINITION`, `VIEW SERVER STATE`.
