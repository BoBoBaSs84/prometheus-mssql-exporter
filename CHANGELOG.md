# Changelog

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
