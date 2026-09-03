# Prometheus MSSQL Exporter Docker Container

[![Node.js CI](https://github.com/awaragi/prometheus-mssql-exporter/actions/workflows/node.js.yml/badge.svg)](https://github.com/awaragi/prometheus-mssql-exporter/actions/workflows/node.js.yml)
[![Docker Pulls](https://img.shields.io/docker/pulls/awaragi/prometheus-mssql-exporter)](https://hub.docker.com/r/awaragi/prometheus-mssql-exporter)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Prometheus exporter for Microsoft SQL Server (MSSQL). A single Node.js process that, on each scrape of `/metrics`, opens a connection, runs a fixed set of queries, and exposes the results as Prometheus gauges.

> Tested against the MSSQL **2019**, **2022** and **2025** Docker images from Microsoft. Other versions likely work but are not covered by CI.

The image is published for `linux/amd64` and `linux/arm64`.

## Exposed metrics

`mssql_up` is always present. The `mssql_*` list below is generated from the queries in `src/metrics.js` — run `npm run metrics` to print each metric together with the SQL that produces it (useful for DBA review).

- `mssql_up` UP status (1 = the scrape connected and ran, 0 = it failed)
- `mssql_product_version` Instance version as `Major.Minor`
- `mssql_instance_local_time` Seconds since epoch on the instance
- `mssql_connections{database,state}` Active connections
- `mssql_client_connections{client,database}` Active client connections
- `mssql_deadlocks` Lock requests per second that ended in a deadlock since last restart
- `mssql_user_errors` User errors/sec since last restart
- `mssql_kill_connection_errors` Kill-connection errors/sec since last restart
- `mssql_database_state{database}` Database state: 0=ONLINE 1=RESTORING 2=RECOVERING 3=RECOVERY_PENDING 4=SUSPECT 5=EMERGENCY 6=OFFLINE 7=COPYING 10=OFFLINE_SECONDARY
- `mssql_log_growths{database}` Times the transaction log has been expanded since last restart
- `mssql_database_filesize{database,logicalname,type,filename}` Physical file sizes in KB (type: 0=rows, 1=log, 2=filestream, 3=n/a, 4=fulltext)
- `mssql_page_read_total` Page reads/sec
- `mssql_page_write_total` Page writes/sec
- `mssql_page_life_expectancy` Minimum seconds a page stays in the buffer pool without references (Microsoft's traditional guidance: keep above 300)
- `mssql_lazy_write_total` Lazy writes/sec
- `mssql_page_checkpoint_total` Checkpoint pages/sec
- `mssql_io_stall{database,type}` I/O stall wait time (ms) since last restart, per `type` (`read`, `write`, `queued_read`, `queued_write`)
- `mssql_io_stall_total{database}` Total I/O stall wait time (ms) since last restart
- `mssql_batch_requests` Transact-SQL batches received per second
- `mssql_transactions{database}` Transactions started per second per database (excludes XTP-only transactions)
- `mssql_page_fault_count` Page faults since last restart
- `mssql_memory_utilization_percentage` Memory utilization percentage
- `mssql_total_physical_memory_kb` Total physical memory in KB
- `mssql_available_physical_memory_kb` Available physical memory in KB
- `mssql_total_page_file_kb` Total page file in KB
- `mssql_available_page_file_kb` Available page file in KB

Unless `COLLECT_DEFAULT_METRICS=false`, the standard `prom-client` process/Node.js metrics (`process_*`, `nodejs_*`) are also exposed.

Please feel free to submit other interesting metrics to include.

## Usage

```shell
docker run -e SERVER=192.168.56.101 -e USERNAME=SA -e PASSWORD=qkD4x3yy -e DEBUG=app \
  -p 4000:4000 --name prometheus-mssql-exporter awaragi/prometheus-mssql-exporter
```

The container exposes port 4000 and serves metrics at `/metrics` (`/` redirects there). `/healthz` is a database-free liveness probe used by the image `HEALTHCHECK`.

### Configuration

All configuration is via environment variables:

| Variable                   | Required | Default | Description                                                                                                                                                            |
| -------------------------- | -------- | ------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SERVER`                   | yes      | —       | Server IP or DNS name                                                                                                                                                  |
| `USERNAME`                 | yes      | —       | Login used to connect                                                                                                                                                  |
| `PASSWORD`                 | yes      | —       | Login password                                                                                                                                                         |
| `PORT`                     | no       | `1433`  | SQL Server TCP port                                                                                                                                                    |
| `ENCRYPT`                  | no       | `true`  | [`encrypt`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnectionstringbuilder.encrypt) connection setting                               |
| `TRUST_SERVER_CERTIFICATE` | no       | `true`  | [`trustServerCertificate`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnectionstringbuilder.trustservercertificate) connection setting |
| `EXPOSE`                   | no       | `4000`  | Port the exporter's web server listens on                                                                                                                              |
| `COLLECT_DEFAULT_METRICS`  | no       | `true`  | Also expose `prom-client` default process/Node.js metrics                                                                                                              |
| `DEBUG`                    | no       | —       | Comma-delimited log channels: `app`, `db`, `metrics`, `queries`                                                                                                        |

The connecting login needs:

```sql
GRANT VIEW ANY DEFINITION TO <user>;
GRANT VIEW SERVER STATE TO <user>;
```

### docker compose

```yaml
services:
  mssql-exporter:
    image: awaragi/prometheus-mssql-exporter
    ports:
      - "4000:4000"
    environment:
      SERVER: mssql
      USERNAME: SA
      PASSWORD: ${SA_PASSWORD}
      DEBUG: app
```

### Prometheus scrape config

```yaml
scrape_configs:
  - job_name: mssql
    static_configs:
      - targets: ["mssql-exporter:4000"]
```

Run each additional instance on its own forwarded port (`-p 4001:4000`, `-p 4002:4000`, ...).

## Frequently Asked Questions (FAQ)

### Unable to connect to database

Raised in [issue #19](https://github.com/awaragi/prometheus-mssql-exporter/issues/19). Named instances assign the TCP port dynamically by default; either configure a [static port](https://learn.microsoft.com/en-US/sql/database-engine/configure-windows/configure-a-server-to-listen-on-a-specific-tcp-port) and pass it via `PORT`, or enable the SQL Server Browser.

### Running multiple instances of the exporter

Raised in [issue #20](https://github.com/awaragi/prometheus-mssql-exporter/issues/20). Give each container its own host port forward.

### What Grafana dashboard can I use

- https://grafana.com/grafana/dashboards/13919

If you maintain a dashboard and want it listed here, open a PR.

### Running in the background

`docker run -d ...`

## Development

Requires Node.js **>= 20.12** and (for the e2e suite and local SQL Server) Docker.

```shell
npm ci
```

### Run a local SQL Server

```shell
export MSSQL_SA_PASSWORD='Str0ng_Passw0rd!'
npm run test:mssql:2022   # also test:mssql:2019 / test:mssql:2025
```

Add `-v /<mypath>:/var/opt/mssql/data` for persistent storage.

### Run the exporter

Copy `.env.example` to `.env`, adjust it, then:

```shell
npm start        # loads .env automatically
npm run dev      # same, with --watch
```

Fetch metrics:

```shell
curl http://localhost:4000/metrics
```

### List all metrics and their queries

```shell
npm run metrics
```

## Testing

- `npm test` / `npm run test:unit` — fast unit tests (collectors, config, server with a stubbed DB). No setup required.
- `npm run test:e2e` — boots a real SQL Server in a throwaway container via [Testcontainers](https://node.testcontainers.org/), scrapes it in-process, and asserts every declared metric family is produced. Requires a container runtime (Docker or Podman); automatically skipped when none is reachable. Override the image with `MSSQL_IMAGE=mcr.microsoft.com/mssql/server:2019-latest`.
- `npm run coverage` — unit tests with a V8 coverage report.
- `npm run lint` / `npm run format` — ESLint / Prettier.

CI runs the unit tests on Node 20/22/24 and the e2e suite against MSSQL 2019, 2022 and 2025.

### Running the e2e suite with Podman

```shell
export DOCKER_HOST='npipe:////./pipe/podman-machine-default'   # Windows; use the unix socket on Linux/macOS
export TESTCONTAINERS_RYUK_DISABLED=true
npm run test:e2e
```

## Building and publishing the image

```shell
npm run docker:build
# multi-arch build (as CI does on a published release)
docker buildx build --platform linux/amd64,linux/arm64 -t awaragi/prometheus-mssql-exporter .
```

Publishing to Docker Hub is handled by `.github/workflows/publish.yml` on a published GitHub Release.
