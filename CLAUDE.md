# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```shell
dotnet restore MssqlExporter.slnx
dotnet build MssqlExporter.slnx          # warnings are errors
dotnet run --project src/MssqlExporter   # run exporter (needs MSSQL_SERVER / MSSQL_USER / MSSQL_PASSWORD)
dotnet run --project src/MssqlExporter -- --print-metrics   # every collector's gauges + the exact SQL (authoritative metric list)

dotnet format MssqlExporter.slnx         # CI runs it with --verify-no-changes

dotnet test tests/MssqlExporter.Tests               # unit tests
dotnet test tests/MssqlExporter.E2E.Tests           # Testcontainers e2e; needs Docker or Podman
```

Single test class / single test:

```shell
dotnet test tests/MssqlExporter.Tests --filter "FullyQualifiedName~CollectorsTests"
dotnet test tests/MssqlExporter.Tests --filter "Name=MssqlProductVersionRecordsMajorMinor"
```

Local SQL Server for manual testing:

```shell
docker run --rm -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Str0ng_Passw0rd!' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
```

E2E image override: `MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2019-latest dotnet test tests/MssqlExporter.E2E.Tests`.
With Podman, Testcontainers needs a reachable Docker API: set `DOCKER_HOST` (e.g. `npipe://./pipe/podman-machine-default` or a `tcp://` endpoint) and `TESTCONTAINERS_RYUK_DISABLED=true`. The e2e suite self-skips when no container runtime is reachable. Git Bash rewrites `npipe:////` values; set `DOCKER_HOST` from PowerShell.

Stop the build servers (`dotnet build-server shutdown`) before moving or deleting project directories on Windows; they keep handles open.

CI (`.github/workflows/dotnet.yml`) runs format check + unit tests, and e2e against MSSQL 2019/2022/2025 (2025 is `continue-on-error`).

## Architecture

.NET 10 ASP.NET Core minimal-API app. Scrape flow:

`Program.cs` (bootstrap: options from env, default-metric suppression, DI, `--print-metrics`)
→ `Endpoints.cs` (`/`, `/healthz`, `/metrics`, `/probe`)
→ `Scraper.cs` (connect, run every collector, close)
→ `Data/SqlRunner.cs` (`Microsoft.Data.SqlClient` connect + query)
→ `Binding/MetricsBinder.cs` (binds collector descriptions to a prometheus-net registry)
→ `Collectors/*.cs` (the actual SQL + gauge definitions).

Key design points that span files:

- **Collectors are pure descriptions.** Each file in `src/MssqlExporter/Collectors/` exposes an `All` list of `Collector` records; `Collectors.cs` concatenates them into one ordered `Collectors.All`. A collector is:

  ```csharp
  new Collector(
      "mssql_<group>",                 // unique name, also the fixture key
      new Dictionary<string, GaugeSpec> { ["key"] = new("help", Name: "...", LabelNames: [...]) },
      "<single SQL statement>",
      (rows, gauges) => { },           // rows are object?[][]; column order must match the SELECT
      Optional: true);                 // may legitimately return 0 rows (Always On off, no Agent jobs)
  ```

  Collectors never touch prometheus-net or the DB directly. `MetricsBinder` turns `Gauges` into live gauges on **a fresh `CollectorRegistry` per scrape** (both `/metrics` and `/probe`), so a dropped database stops being reported on the next scrape. Keep collectors as a data record; do not turn them into an interface hierarchy (`MetricsDocs` and the fixture test enumerate them as data).

- **Labels are named, not positional.** `gauges.Set("key", value, ("database", db), ("state", "current"))` is checked against the declared `LabelNames` by `GaugeSet`; prometheus-net's own `WithLabels` is positional and silently wrong when reordered.

- **Row shape.** `ISqlSession.RunQueryAsync` materialises `object?[][]` (SQL NULL → `null`). Use `CollectorUtil.AsDouble` for values (`null` → the gauge is left unset; use `?? -1` style defaults where the Node behaviour was explicit) and `AsString` for labels (`null` → `"null"`, matching the Node exporter). `CollectorUtil.ByName` / `RatioPercent` cover the perf-counter collectors.

- **Failure isolation.** `Scraper` runs collectors sequentially on one connection. A query error, timeout or empty result set is logged and skipped — never fatal — and recorded as `mssql_collector_success{collector=...} 0` plus `mssql_collector_duration_seconds`. Only a failed _connection_ short-circuits `/metrics`, which then returns just `mssql_up 0` with a sanitised `X-Error` header. `RequestAborted` cancels a scrape; cancellation is not recorded as a collector failure.

- **DB injection for tests.** `ISqlRunner` / `ISqlSession` are registered in DI; `tests/MssqlExporter.Tests/FakeSqlRunner.cs` replaces them through `WebApplicationFactory<Program>` so `EndpointsTests` exercises the full HTTP path. Tests also override the `ExporterOptions` singleton.

- **Config is env-only** (`ExporterOptions.From(env)`): `MSSQL_SERVER`/`MSSQL_USER`/`MSSQL_PASSWORD` are required, everything else has a default. The old `SERVER`/`USERNAME`/`PASSWORD`/`PORT` names are intentionally not read (Windows always defines `USERNAME`). `ParseBool`/`ParseInt` keep the old Node semantics. Query/connect timeouts are milliseconds in config but whole seconds in SqlClient (rounded up). See `.env.example` and the README table.

- **E2E provisioning.** One container per run, started with `MSSQL_AGENT_ENABLED=true`. `tests/MssqlExporter.E2E.Tests/Provisioner.cs` runs the `GO`-separated fixtures in `tests/MssqlExporter.E2E.Tests/Sql/` (SqlClient has no `GO`, so batches are split client-side), waits for the Agent service and runs a job once so `sysjobhistory` has the `run_date`/`run_time` that `msdb.dbo.agent_datetime()` is called with.

- **Docker image.** `Dockerfile` publishes onto `dotnet/aspnet:10.0-alpine`, runs as the non-root `app` user and installs `icu-libs`: `Microsoft.Data.SqlClient` does not support globalization invariant mode, which the Alpine image enables by default.

## Adding or changing a collector

1. Add/edit the collector in the appropriate `src/MssqlExporter/Collectors/*.cs` (or a new file registered in `Collectors.cs`).
2. Add a matching entry to `tests/MssqlExporter.Tests/Fixtures/Rows.cs` — `CollectorsTests.HasAFixtureForEveryCollector` asserts fixture keys equal collector names, so a missing fixture fails the build.
3. Mark `Optional: true` if the query can legitimately return no rows on a vanilla instance; the e2e suite requires every non-optional gauge family to be present in the scrape output.
4. If the collector reads `msdb` (backups, suspect pages, Agent) or needs elevated rights, update **both** the "Required permissions" section of the README and `tests/MssqlExporter.E2E.Tests/Sql/exporter-permissions.sql` — the e2e suite scrapes with that login and compares its per-collector success against `sa`, so a missing grant fails the build.
5. New metrics use Prometheus base units (`_bytes`, `_seconds`) and `_total` for cumulative counters; pre-2.1.0 metrics keep their historical `_kb` names for compatibility.
6. SQL goes in raw string literals (`"""`) with the content at column 0, so it stays byte-identical to what runs on the server.

Logging uses the `App`, `Db` and `Metrics` categories (set e.g. `Logging__LogLevel__App=Debug`).
