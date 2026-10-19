# Prometheus MSSQL Exporter Docker Container

[![.NET CI](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/dotnet.yml/badge.svg)](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/dotnet.yml)
[![Build and Publish](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/publish.yml/badge.svg)](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/publish.yml)
[![CodeQL](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/github-code-scanning/codeql)
[![Dependabot Updates](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/dependabot/dependabot-updates/badge.svg)](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/actions/workflows/dependabot/dependabot-updates)
[![Container image](https://img.shields.io/badge/ghcr.io-prometheus--mssql--exporter-2496ED?logo=docker&logoColor=white)](https://github.com/BoBoBaSs84/prometheus-mssql-exporter/pkgs/container/prometheus-mssql-exporter)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Prometheus exporter for Microsoft SQL Server (MSSQL). A single .NET 10 process that, on each scrape of `/metrics`, opens a connection, runs each collector's query, and exposes the results as Prometheus gauges. Collectors live in `src/MssqlExporter/Collectors/` and each only _describes_ its gauges and SQL, so the same set can also be run per-target through the `/probe` endpoint.

> Tested against the MSSQL **2019**, **2022** and **2025** Docker images from Microsoft. Other versions likely work but are not covered by CI.

The image is published for `linux/amd64` and `linux/arm64`.

## Exposed metrics

Run `dotnet run --project src/MssqlExporter -- --print-metrics` (or `docker run --rm ghcr.io/bobobass84/prometheus-mssql-exporter --print-metrics`) for the authoritative list — every metric with the exact SQL that produces it (useful for DBA review). 89 `mssql_*` families come from the collectors, plus 3 scrape-quality metrics, by area:

- **Instance** — `mssql_up`, `mssql_product_version`, `mssql_instance_local_time`
- **Connections & sessions** — `mssql_connections{database,state}`, `mssql_client_connections{client,database}`, `mssql_active_sessions{status}`, `mssql_active_requests`
- **Activity & blocking** — `mssql_blocked_sessions`, `mssql_blocking_wait_seconds_max`, `mssql_longest_running_request_seconds`, `mssql_oldest_active_transaction_seconds`
- **Wait statistics** — `mssql_wait_time_ms{wait_type}`, `mssql_wait_tasks{wait_type}`, `mssql_signal_wait_time_ms{wait_type}` (top 25, benign waits excluded), `mssql_wait_time_ms_total`
- **Throughput** (cumulative — use `rate()`) — `mssql_batch_requests`, `mssql_transactions{database}`, `mssql_sql_compilations_total`, `mssql_sql_recompilations_total`, `mssql_page_splits_total`, `mssql_full_scans_total`, `mssql_range_scans_total`, `mssql_index_searches_total`, `mssql_forwarded_records_total`, `mssql_lock_waits_total`, `mssql_lock_wait_time_ms_total`, `mssql_lock_timeouts_total`, `mssql_latch_waits_total`, `mssql_latch_wait_time_ms_total`, `mssql_deadlocks`
- **Memory** — `mssql_target_server_memory_bytes`, `mssql_total_server_memory_bytes`, `mssql_memory_grants_pending`, `mssql_memory_grants_outstanding`, `mssql_granted_workspace_memory_bytes`, `mssql_maximum_workspace_memory_bytes`, `mssql_connection_memory_bytes`, `mssql_lock_memory_bytes`, `mssql_optimizer_memory_bytes`, `mssql_sql_cache_memory_bytes`, `mssql_buffer_cache_hit_ratio`, `mssql_plan_cache_hit_ratio`, plus OS memory (`mssql_*_physical_memory_kb`, `mssql_*_page_file_kb`, `mssql_memory_utilization_percentage`, `mssql_page_fault_count`)
- **Buffer manager & I/O** — `mssql_page_read_total`, `mssql_page_write_total`, `mssql_page_life_expectancy`, `mssql_lazy_write_total`, `mssql_page_checkpoint_total`, `mssql_io_stall{database,type}`, `mssql_io_stall_total{database}`
- **Storage** — `mssql_database_filesize{...}`, `mssql_database_log_size_bytes{database}`, `mssql_database_log_used_percent{database}`, `mssql_database_vlf_count{database}`, `mssql_volume_total_bytes{volume}`, `mssql_volume_available_bytes{volume}`, `mssql_log_growths{database}`
- **Databases** — `mssql_database_state{database}`, `mssql_database_recovery_model{database}`, `mssql_database_is_read_only{database}`, `mssql_database_is_auto_close_on{database}`, `mssql_database_is_auto_shrink_on{database}`, `mssql_database_page_verify_option{database}`, `mssql_suspect_pages{database}`
- **Backups** — `mssql_database_backup_age_seconds{database,type}` (-1 = never), `mssql_database_last_backup_timestamp{database,type}` — `type` ∈ `full`, `diff`, `log`
- **Configuration** — `mssql_configuration{name}` (a curated subset of `sys.configurations`)
- **Errors** — `mssql_user_errors`, `mssql_kill_connection_errors`
- **Always On** (only `mssql_hadr_enabled` is emitted when AGs are not configured) — `mssql_hadr_replica_*{ag,replica}`, `mssql_hadr_database_*{ag,replica,database}`, `mssql_hadr_secondary_lag_seconds{...}`
- **SQL Agent** (emitted only when Agent/jobs exist) — `mssql_agent_up`, `mssql_agent_job_enabled{job}`, `mssql_agent_job_last_run_success{job}`, `mssql_agent_job_last_run_timestamp{job}`, `mssql_agent_job_last_duration_seconds{job}`, `mssql_agent_job_running{job}`
- **Scrape quality** — `mssql_scrape_duration_seconds`, `mssql_collector_duration_seconds{collector}`, `mssql_collector_success{collector}`

Unless `COLLECT_DEFAULT_METRICS=false`, the standard .NET runtime and process metrics (`process_*`, `dotnet_*`) are also exposed on `/metrics` (never on `/probe`).

> Existing metrics keep their historical `_kb` units; metrics added in 2.1.0 use Prometheus base units (`_bytes`, `_seconds`) and the `_total` suffix for cumulative counters.

Please feel free to submit other interesting metrics to include.

## Usage

```shell
docker run -e MSSQL_SERVER=192.168.56.101 -e MSSQL_USER=SA -e MSSQL_PASSWORD=qkD4x3yy -e Logging__LogLevel__App=Debug \
  -p 4000:4000 --name prometheus-mssql-exporter ghcr.io/bobobass84/prometheus-mssql-exporter
```

The container exposes port 4000. Endpoints: `/metrics` (the configured instance; `/` redirects here), `/probe?target=host[:port]` (any instance — see below), `/healthz` (database-free liveness probe used by the image `HEALTHCHECK`).

### Configuration

All configuration is via environment variables:

| Variable                        | Required | Default       | Description                                                                                                                                                            |
| ------------------------------- | -------- | ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `MSSQL_SERVER`                  | yes      | —             | Server IP or DNS name                                                                                                                                                  |
| `MSSQL_USER`                    | yes      | —             | Login used to connect                                                                                                                                                  |
| `MSSQL_PASSWORD`                | yes      | —             | Login password                                                                                                                                                         |
| `MSSQL_PORT`                    | no       | `1433`        | SQL Server TCP port                                                                                                                                                    |
| `ENCRYPT`                       | no       | `true`        | [`Encrypt`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnectionstringbuilder.encrypt) connection setting                               |
| `TRUST_SERVER_CERTIFICATE`      | no       | `true`        | [`TrustServerCertificate`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnectionstringbuilder.trustservercertificate) connection setting |
| `EXPOSE`                        | no       | `4000`        | Port the exporter's web server listens on                                                                                                                              |
| `COLLECT_DEFAULT_METRICS`       | no       | `true`        | Also expose the .NET runtime/process metrics                                                                                                                           |
| `CONNECT_TIMEOUT_MS`            | no       | `15000`       | Connection timeout, rounded up to whole seconds                                                                                                                        |
| `QUERY_TIMEOUT_MS`              | no       | `30000`       | Per-collector query timeout, rounded up to whole seconds — a slow collector is abandoned and skipped, the rest of the scrape still returns                             |
| `PROBE_ENABLED`                 | no       | `true`        | Serve the `/probe` multi-target endpoint                                                                                                                               |
| `Logging__LogLevel__<Category>` | no       | `Information` | Standard ASP.NET Core log level per category: `App`, `Db`, `Metrics` (e.g. `Logging__LogLevel__App=Debug`)                                                             |

> The generic `USERNAME` and `PASSWORD` variables were replaced by `MSSQL_USER` / `MSSQL_PASSWORD` (and `SERVER`/`PORT` by `MSSQL_SERVER`/`MSSQL_PORT`): Windows always defines `USERNAME`, which would silently authenticate as the wrong user. The old names are no longer read.

### Required permissions

Create a dedicated least-privilege login. Base rights (cover all the always-on DMV collectors):

```sql
GRANT VIEW ANY DEFINITION TO [exporter];
GRANT VIEW SERVER STATE TO [exporter];
```

For the backup, suspect-page and SQL Agent collectors the login also needs read access to `msdb`:

```sql
USE msdb;
CREATE USER [exporter] FOR LOGIN [exporter];
ALTER ROLE [db_datareader] ADD MEMBER [exporter];   -- backupset, suspect_pages, sysjobhistory, ...
ALTER ROLE [SQLAgentReaderRole] ADD MEMBER [exporter];   -- sysjobs / sysjobactivity (optional)
GRANT EXECUTE ON [dbo].[agent_datetime] TO [exporter];
```

Collectors whose permissions are missing simply fail and are skipped (`mssql_collector_success{collector="..."} 0`); the rest of the scrape is unaffected.

The same statements live in [`tests/MssqlExporter.E2E.Tests/Sql/exporter-permissions.sql`](tests/MssqlExporter.E2E.Tests/Sql/exporter-permissions.sql) (with `__LOGIN__` / `__PASSWORD__` placeholders). The e2e suite provisions that exact login and asserts it loses no collector against `sa`, so this list is verified on every CI run.

### docker compose

```yaml
services:
  mssql-exporter:
    image: ghcr.io/bobobass84/prometheus-mssql-exporter
    ports:
      - "4000:4000"
    environment:
      MSSQL_SERVER: mssql
      MSSQL_USER: SA
      MSSQL_PASSWORD: ${SA_PASSWORD}
      Logging__LogLevel__App: Debug
```

### Prometheus scrape config

```yaml
scrape_configs:
  - job_name: mssql
    static_configs:
      - targets: ["mssql-exporter:4000"]
```

### Monitoring several instances from one exporter (`/probe`)

`GET /probe?target=host[:port]` connects to `host` using the exporter's configured credentials
and returns that instance's metrics on an isolated registry (concurrent probes for different
targets do not interfere). `MSSQL_SERVER`/`MSSQL_PORT` still have to be set for startup — point them at any
one reachable instance, or a dummy. Set `PROBE_ENABLED=false` to disable the endpoint.

```yaml
scrape_configs:
  - job_name: mssql-probe
    metrics_path: /probe
    static_configs:
      - targets:
          - sql-prod-1:1433
          - sql-prod-2:1433
          - sql-dr-1:1433
    relabel_configs:
      - source_labels: [__address__]
        target_label: __param_target
      - source_labels: [__param_target]
        target_label: instance
      - target_label: __address__
        replacement: mssql-exporter:4000
```

Otherwise run one exporter per instance, each on its own forwarded port (`-p 4001:4000`, `-p 4002:4000`, ...).

## Frequently Asked Questions (FAQ)

### Unable to connect to database

Named instances assign the TCP port dynamically by default; either configure a [static port](https://learn.microsoft.com/en-US/sql/database-engine/configure-windows/configure-a-server-to-listen-on-a-specific-tcp-port) and pass it via `MSSQL_PORT`, or enable the SQL Server Browser.

### Running multiple instances of the exporter

Give each container its own host port forward.

### What Grafana dashboard can I use

- https://grafana.com/grafana/dashboards/13919

If you maintain a dashboard and want it listed here, open a PR.

### Running in the background

`docker run -d ...`

## Development

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download) and (for the e2e suite and a local SQL Server) a container runtime — Docker or Podman.

```shell
dotnet restore MssqlExporter.slnx
dotnet build MssqlExporter.slnx
```

### Run a local SQL Server

```shell
docker run --rm -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD='Str0ng_Passw0rd!' -p 1433:1433 \
  mcr.microsoft.com/mssql/server:2022-latest   # also 2019-latest / 2025-latest
```

Add `-v /<mypath>:/var/opt/mssql/data` for persistent storage.

### Run the exporter

Set the required variables (see `.env.example`), then:

```shell
export MSSQL_SERVER=localhost MSSQL_USER=SA MSSQL_PASSWORD='Str0ng_Passw0rd!'
dotnet run --project src/MssqlExporter
```

Fetch metrics:

```shell
curl http://localhost:4000/metrics
```

### List all metrics and their queries

```shell
dotnet run --project src/MssqlExporter -- --print-metrics
```

## Testing

- `dotnet test tests/MssqlExporter.Tests` — fast unit tests (collectors, config, endpoints with a stubbed DB). No setup required.
- `dotnet test tests/MssqlExporter.E2E.Tests` — boots a real SQL Server in a throwaway container via [Testcontainers](https://dotnet.testcontainers.org/), scrapes it in-process, and asserts every declared metric family is produced. Requires a container runtime (Docker or Podman); automatically skipped when none is reachable. Override the image with `MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2019-latest`.

  The container runs with `MSSQL_AGENT_ENABLED=true` and the suite provisions the documented least-privilege login plus a SQL Agent job, so the scrape is also performed as `exporter` — any grant missing from [Required permissions](#required-permissions) shows up as a failing collector. A second login without `GRANT EXECUTE ON [dbo].[agent_datetime]` guards that particular grant against regressing.

- `dotnet format MssqlExporter.slnx` — code formatting (CI runs it with `--verify-no-changes`).

CI runs the format check and unit tests, and the e2e suite against MSSQL 2019, 2022 and 2025.

### Running the e2e suite with Podman

Testcontainers for .NET needs a reachable Docker-compatible API. With the Podman machine running, expose it on a named pipe or TCP endpoint, then:

```shell
export DOCKER_HOST='npipe://./pipe/podman-machine-default'   # or tcp://127.0.0.1:2375; the unix socket on Linux/macOS
export TESTCONTAINERS_RYUK_DISABLED=true
dotnet test tests/MssqlExporter.E2E.Tests
```

## Building and publishing the image

```shell
docker build -t ghcr.io/bobobass84/prometheus-mssql-exporter .
# multi-arch build (as CI does on a published release)
docker buildx build --platform linux/amd64,linux/arm64 -t ghcr.io/bobobass84/prometheus-mssql-exporter .
```

Publishing to the GitHub Container Registry is handled by `.github/workflows/publish.yml` on a published GitHub Release.
