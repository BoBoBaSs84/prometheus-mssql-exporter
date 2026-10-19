using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>
/// Always On availability group health. Only <c>mssql_hadr_enabled</c> is always
/// present; the rest emit nothing unless the instance participates in an AG.
/// </summary>
internal static class AlwaysOn
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_hadr_enabled",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_hadr_enabled"] = new("1 if the Always On availability groups feature is enabled"),
            },
            "SELECT CAST(ISNULL(SERVERPROPERTY('IsHadrEnabled'), 0) AS INT)",
            (rows, gauges) => gauges.Set("mssql_hadr_enabled", CollectorUtil.AsDouble(rows[0][0]))),

        new(
            "mssql_hadr_replica",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_hadr_replica_role"] = new("Replica role: 1=RESOLVING 2=PRIMARY 3=SECONDARY", LabelNames: ["ag", "replica"]),
                ["mssql_hadr_replica_connected_state"] = new("1 if the replica is connected", LabelNames: ["ag", "replica"]),
                ["mssql_hadr_replica_synchronization_health"] = new(
                    "Replica sync health: 0=NOT_HEALTHY 1=PARTIALLY_HEALTHY 2=HEALTHY",
                    LabelNames: ["ag", "replica"]),
            },
            """
SELECT ag.name, ar.replica_server_name, ars.role, ars.connected_state, ars.synchronization_health
FROM sys.dm_hadr_availability_replica_states ars
JOIN sys.availability_replicas ar ON ar.replica_id = ars.replica_id
JOIN sys.availability_groups ag ON ag.group_id = ars.group_id
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    (string, string)[] labels = [("ag", CollectorUtil.AsString(row[0])), ("replica", CollectorUtil.AsString(row[1]))];
                    gauges.Set("mssql_hadr_replica_role", CollectorUtil.AsDouble(row[2]), labels);
                    gauges.Set("mssql_hadr_replica_connected_state", CollectorUtil.AsDouble(row[3]), labels);
                    gauges.Set("mssql_hadr_replica_synchronization_health", CollectorUtil.AsDouble(row[4]), labels);
                }
            },
            Optional: true),

        new(
            "mssql_hadr_database",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_hadr_database_synchronization_health"] = new(
                    "Database sync health: 0=NOT_HEALTHY 1=PARTIALLY_HEALTHY 2=HEALTHY",
                    LabelNames: ["ag", "replica", "database"]),
                ["mssql_hadr_database_synchronization_state"] = new(
                    "Database sync state: 0=NOT SYNCHRONIZING 1=SYNCHRONIZING 2=SYNCHRONIZED 3=REVERTING 4=INITIALIZING",
                    LabelNames: ["ag", "replica", "database"]),
                ["mssql_hadr_log_send_queue_bytes"] = new("Log records not yet sent to the secondary", LabelNames: ["ag", "replica", "database"]),
                ["mssql_hadr_redo_queue_bytes"] = new("Log records not yet redone on the secondary", LabelNames: ["ag", "replica", "database"]),
                ["mssql_hadr_secondary_lag_seconds"] = new("Estimated secondary replica lag in seconds", LabelNames: ["ag", "replica", "database"]),
            },
            """
SELECT ag.name, ar.replica_server_name, DB_NAME(drs.database_id),
  drs.synchronization_health, drs.synchronization_state,
  ISNULL(drs.log_send_queue_size, 0) * 1024, ISNULL(drs.redo_queue_size, 0) * 1024, ISNULL(drs.secondary_lag_seconds, 0)
FROM sys.dm_hadr_database_replica_states drs
JOIN sys.availability_replicas ar ON ar.replica_id = drs.replica_id
JOIN sys.availability_groups ag ON ag.group_id = drs.group_id
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    (string, string)[] labels =
                    [
                        ("ag", CollectorUtil.AsString(row[0])),
                        ("replica", CollectorUtil.AsString(row[1])),
                        ("database", CollectorUtil.AsString(row[2])),
                    ];
                    gauges.Set("mssql_hadr_database_synchronization_health", CollectorUtil.AsDouble(row[3]), labels);
                    gauges.Set("mssql_hadr_database_synchronization_state", CollectorUtil.AsDouble(row[4]), labels);
                    gauges.Set("mssql_hadr_log_send_queue_bytes", CollectorUtil.AsDouble(row[5]), labels);
                    gauges.Set("mssql_hadr_redo_queue_bytes", CollectorUtil.AsDouble(row[6]), labels);
                    gauges.Set("mssql_hadr_secondary_lag_seconds", CollectorUtil.AsDouble(row[7]), labels);
                }
            },
            Optional: true),
    ];
}
