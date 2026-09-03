# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```shell
npm ci                 # install
npm start              # run exporter (auto-loads .env)
npm run dev            # same, with --watch
npm run metrics        # print every collector's gauges + the exact SQL (authoritative metric list)

npm run lint           # eslint .
npm run format         # prettier --write .
npm run format:check   # prettier --check . (printWidth 160)

npm test               # unit tests only (vitest, project "unit")
npm run test:e2e       # Testcontainers e2e; needs Docker or Podman
npm run coverage       # unit + V8 coverage
npm run test:watch     # unit tests in watch mode
```

Single test file / single test:

```shell
npx vitest run --project unit test/unit/collectors.test.js
npx vitest run --project unit -t "mssql_product_version records major.minor"
```

Local SQL Server for manual testing (`MSSQL_SA_PASSWORD` must be exported):

```shell
npm run test:mssql:2022   # also :2019 / :2025
```

E2E image override: `MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2019-latest npm run test:e2e`.
With Podman on Windows also set `DOCKER_HOST='npipe:////./pipe/podman-machine-default'` and `TESTCONTAINERS_RYUK_DISABLED=true`. The e2e suite self-skips when no container runtime is reachable.

CI (`.github/workflows/node.js.yml`) runs lint + unit on Node 20/22/24 and e2e against MSSQL 2019/2022/2025 (2025 is `continue-on-error`).

## Architecture

ESM-only Node app (`"type": "module"`, Node >= 20.12). Scrape flow:

`src/index.js` (bootstrap: config, default metrics, listen, SIGINT/SIGTERM drain)
→ `src/server.js` (Express app; `/metrics`, `/probe`, `/healthz`, `/` redirect)
→ `src/db.js` (tedious connect + `runQuery`)
→ `src/metrics.js` (binds collector descriptions to a prom-client registry)
→ `src/collectors/*.js` (the actual SQL + gauge definitions).

Key design points that span files:

- **Collectors are pure descriptions.** Each file in `src/collectors/` default-exports an _array_ of collector objects; `src/collectors/index.js` spreads them into one ordered `collectors` array. A collector is:

  ```js
  { name: "mssql_<group>",          // unique key, also the entries[] key
    optional: true,                 // may legitimately return 0 rows (Always On off, no Agent jobs)
    gauges: { key: { name?, help, labelNames? } },
    query: "<single SQL statement>",
    collect: (rows, metrics) => {} }
  ```

  Collectors never touch prom-client or the DB directly. `metrics.js` turns `gauges` into live `client.Gauge` instances, so the same collector set can be materialised against the default registry (`/metrics`) _or_ a throwaway per-request registry (`/probe`). That is why `buildEntries(registry)` / `buildMeta(registry)` exist alongside the default-registry `entries` / `meta` exports.

- **Row shape.** tedious rows are `rows[i][columnIndex].value`; column order in `collect` must match the `SELECT` order in `query`. `db.coerceRow` converts `bigint` and integer-looking strings to numbers (prom-client rejects the former); non-numeric strings (database names, versions, paths) pass through untouched.

- **Failure isolation.** `collectAll` in `server.js` runs collectors sequentially on one connection. A query error, timeout or empty result set is logged and skipped — never fatal — and recorded as `mssql_collector_success{collector=...} 0` plus `mssql_collector_duration_seconds`. Only a failed _connection_ short-circuits the request, which then returns just `mssql_up 0` with an `X-Error` header.

- **DB injection for tests.** `createApp(config, deps)` merges `deps` over `src/db.js`, so `test/unit/server.test.js` exercises the full HTTP path with a stubbed `connect`/`runQuery`.

- **Config is env-only** (`src/config.js`, `loadConfig(env)`), returning a tedious `ConnectionConfiguration` plus exporter settings. `SERVER`/`USERNAME`/`PASSWORD` are required; everything else has a default. See `.env.example` and the README table.

## Adding or changing a collector

1. Add/edit the collector object in the appropriate `src/collectors/*.js` (or a new file registered in `collectors/index.js`).
2. Add a matching entry to `test/fixtures/rows.js` — `test/unit/collectors.test.js` asserts fixture keys exactly equal collector keys, so a missing fixture fails the build.
3. Mark `optional: true` if the query can legitimately return no rows on a vanilla instance; the e2e suite requires every non-optional gauge family to be present in the scrape output.
4. If the collector reads `msdb` (backups, suspect pages, Agent) or needs elevated rights, update the "Required permissions" section of the README.
5. New metrics use Prometheus base units (`_bytes`, `_seconds`) and `_total` for cumulative counters; pre-2.1.0 metrics keep their historical `_kb` names for compatibility.

Debug logging uses `debug` channels `app`, `db`, `metrics` (set via `DEBUG`).
