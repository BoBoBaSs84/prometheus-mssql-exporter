# Changelog

## 2.1.0

Additive release — no breaking changes, `/metrics` remains backward compatible.

### Added

- ~60 new `mssql_*` metric families: SQL-engine memory (target/total server memory,
  memory grants pending, workspace memory, cache hit ratios), throughput counters
  (compilations, page splits, scans, lock/latch waits), live activity (blocked
  sessions, longest-running request, oldest transaction), top-25 wait statistics,
  transaction-log space + VLF counts, volume free space, per-database backup age and
  properties, suspect pages, a curated `sys.configurations` subset, Always On AG
  health, and SQL Agent job outcomes.
- Scrape-quality metrics: `mssql_scrape_duration_seconds`,
  `mssql_collector_duration_seconds{collector}`, `mssql_collector_success{collector}`.
- `GET /probe?target=host[:port]` — scrape any instance through one exporter, on an
  isolated per-request registry (blackbox-exporter style). `PROBE_ENABLED` toggles it.
- `QUERY_TIMEOUT_MS` (default 30000) — per-collector query timeout; a slow collector
  is abandoned and skipped without failing the scrape. `CONNECT_TIMEOUT_MS` (15000).

### Changed

- `src/metrics.js` split into `src/collectors/*.js` domain modules. Collectors are now
  declarative (`{ name, gauges, query, collect, optional }`) and bound to a registry by
  `buildEntries(registry)`, so `/metrics` and `/probe` share one collector set.
- The e2e test asserts `(required) ⊆ scraped ⊆ declared` — `optional` collectors
  (Always On, Agent) may be absent on a vanilla instance.

### Permissions

- Backup / suspect-page / Agent collectors need `msdb` read (`db_datareader`, and
  `SQLAgentReaderRole` for jobs). See README "Required permissions". Missing grants
  only disable the affected collector.

## 2.0.0

Modernization release. The `/metrics` output is backward compatible; the breaking
changes are in the runtime and tooling.

### Breaking

- Requires Node.js >= 20.12 (was Node 16). Docker image base is now `node:22-alpine`.
- Codebase converted to ES modules.
- `prom-client` upgraded 9 → 15: `mssql_product_version` is now emitted as a real
  number (e.g. `15` for `15.0`) instead of a string — previously it relied on
  loose coercion that newer `prom-client` rejects.
- CI test matrix drops SQL Server 2017 and adds 2019 / 2022 / 2025.

### Added

- `COLLECT_DEFAULT_METRICS` env var (default `true`) — exposes `prom-client`
  default `process_*` / `nodejs_*` metrics.
- `/healthz` liveness endpoint (no database access); used by the image `HEALTHCHECK`.
- `SIGTERM` graceful shutdown (in addition to `SIGINT`).
- Unit test suite (collectors, config, server) plus a Testcontainers-based e2e
  suite that needs no manual setup. Test runner is now Vitest.
- ESLint (flat config), `.env.example`, multi-arch (`amd64` + `arm64`) image builds.

### Changed

- Dependencies bumped to current majors: `express` 4 → 5, `tedious` 14 → 20,
  `debug` 4.3 → 4.4.
- `src/index.js` split into `config.js`, `db.js`, `server.js` and a thin entrypoint.
- npm scripts are now cross-platform (`node --env-file-if-exists`).
- Dockerfile is multi-stage and runs as the non-root `node` user.
- CI replaces the hand-rolled MSSQL service container + backgrounded exporter with
  the Testcontainers e2e suite; publish workflow uses `docker/metadata-action`.

### Fixed

- E2E metric assertion (`expect(array).toBe(array)` could never pass) and implicit
  global leak in the test parser.
- `bigint` columns (returned by `tedious` as strings) are coerced to numbers in
  `db.js`; with `prom-client` 15's stricter `Gauge.set()` this otherwise dropped
  ~10 metrics (`mssql_io_stall`, `mssql_database_filesize`, `mssql_transactions`,
  `mssql_*_memory_kb`, buffer-manager counters, ...).
