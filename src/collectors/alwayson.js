import createDebug from "debug";

const log = createDebug("metrics");

/**
 * Always On availability group health. Only `mssql_hadr_enabled` is always
 * present; the rest emit nothing unless the instance participates in an AG.
 */
export default [
  {
    name: "mssql_hadr_enabled",
    gauges: {
      mssql_hadr_enabled: { help: "1 if the Always On availability groups feature is enabled" },
    },
    query: `SELECT CAST(ISNULL(SERVERPROPERTY('IsHadrEnabled'), 0) AS INT)`,
    collect: (rows, metrics) => {
      log("Fetched IsHadrEnabled", rows[0][0].value);
      metrics.mssql_hadr_enabled.set(rows[0][0].value);
    },
  },
  {
    name: "mssql_hadr_replica",
    optional: true,
    gauges: {
      mssql_hadr_replica_role: { help: "Replica role: 1=RESOLVING 2=PRIMARY 3=SECONDARY", labelNames: ["ag", "replica"] },
      mssql_hadr_replica_connected_state: { help: "1 if the replica is connected", labelNames: ["ag", "replica"] },
      mssql_hadr_replica_synchronization_health: {
        help: "Replica sync health: 0=NOT_HEALTHY 1=PARTIALLY_HEALTHY 2=HEALTHY",
        labelNames: ["ag", "replica"],
      },
    },
    query: `SELECT ag.name, ar.replica_server_name, ars.role, ars.connected_state, ars.synchronization_health
FROM sys.dm_hadr_availability_replica_states ars
JOIN sys.availability_replicas ar ON ar.replica_id = ars.replica_id
JOIN sys.availability_groups ag ON ag.group_id = ars.group_id`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const labels = { ag: row[0].value, replica: row[1].value };
        metrics.mssql_hadr_replica_role.set(labels, row[2].value);
        metrics.mssql_hadr_replica_connected_state.set(labels, row[3].value);
        metrics.mssql_hadr_replica_synchronization_health.set(labels, row[4].value);
      }
    },
  },
  {
    name: "mssql_hadr_database",
    optional: true,
    gauges: {
      mssql_hadr_database_synchronization_health: {
        help: "Database sync health: 0=NOT_HEALTHY 1=PARTIALLY_HEALTHY 2=HEALTHY",
        labelNames: ["ag", "replica", "database"],
      },
      mssql_hadr_database_synchronization_state: {
        help: "Database sync state: 0=NOT SYNCHRONIZING 1=SYNCHRONIZING 2=SYNCHRONIZED 3=REVERTING 4=INITIALIZING",
        labelNames: ["ag", "replica", "database"],
      },
      mssql_hadr_log_send_queue_bytes: { help: "Log records not yet sent to the secondary", labelNames: ["ag", "replica", "database"] },
      mssql_hadr_redo_queue_bytes: { help: "Log records not yet redone on the secondary", labelNames: ["ag", "replica", "database"] },
      mssql_hadr_secondary_lag_seconds: { help: "Estimated secondary replica lag in seconds", labelNames: ["ag", "replica", "database"] },
    },
    query: `SELECT ag.name, ar.replica_server_name, DB_NAME(drs.database_id),
  drs.synchronization_health, drs.synchronization_state,
  ISNULL(drs.log_send_queue_size, 0) * 1024, ISNULL(drs.redo_queue_size, 0) * 1024, ISNULL(drs.secondary_lag_seconds, 0)
FROM sys.dm_hadr_database_replica_states drs
JOIN sys.availability_replicas ar ON ar.replica_id = drs.replica_id
JOIN sys.availability_groups ag ON ag.group_id = drs.group_id`,
    collect: (rows, metrics) => {
      for (const row of rows) {
        const labels = { ag: row[0].value, replica: row[1].value, database: row[2].value };
        metrics.mssql_hadr_database_synchronization_health.set(labels, row[3].value);
        metrics.mssql_hadr_database_synchronization_state.set(labels, row[4].value);
        metrics.mssql_hadr_log_send_queue_bytes.set(labels, row[5].value);
        metrics.mssql_hadr_redo_queue_bytes.set(labels, row[6].value);
        metrics.mssql_hadr_secondary_lag_seconds.set(labels, row[7].value);
      }
    },
  },
];
