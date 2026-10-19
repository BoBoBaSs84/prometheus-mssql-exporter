using MssqlExporter.Binding;

namespace MssqlExporter.Collectors;

/// <summary>
/// SQL Server Agent service and job outcomes. All optional: Agent may not be
/// running and there may be no jobs. Requires read access to msdb job tables
/// (SQLAgentReaderRole or SELECT on msdb.dbo.sysjobs / sysjobhistory / sysjobactivity).
/// </summary>
internal static class Agent
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        new(
            "mssql_agent_up",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_agent_up"] = new("1 if the SQL Server Agent service is running"),
            },
            """
SELECT CASE WHEN status_desc = 'Running' THEN 1 ELSE 0 END
FROM sys.dm_server_services
WHERE servicename LIKE 'SQL Server Agent%'
""",
            (rows, gauges) => gauges.Set("mssql_agent_up", CollectorUtil.AsDouble(rows[0][0])),
            Optional: true),

        new(
            "mssql_agent_jobs",
            new Dictionary<string, GaugeSpec>
            {
                ["mssql_agent_job_enabled"] = new("1 if the job is enabled", LabelNames: ["job"]),
                ["mssql_agent_job_last_run_success"] = new("Last outcome: 1=succeeded 0=failed/other -1=never run", LabelNames: ["job"]),
                ["mssql_agent_job_last_run_timestamp"] = new("Unix timestamp of the last job run (0 = never)", LabelNames: ["job"]),
                ["mssql_agent_job_last_duration_seconds"] = new("Duration of the last job run, in seconds", LabelNames: ["job"]),
                ["mssql_agent_job_running"] = new("1 if the job is currently executing", LabelNames: ["job"]),
            },
            """
WITH last_run AS (
  SELECT job_id, run_status, run_date, run_time, run_duration,
    ROW_NUMBER() OVER (PARTITION BY job_id ORDER BY run_date DESC, run_time DESC) AS rn
  FROM msdb.dbo.sysjobhistory WHERE step_id = 0
),
activity AS (
  SELECT job_id, start_execution_date, stop_execution_date
  FROM msdb.dbo.sysjobactivity
  WHERE session_id = (SELECT MAX(session_id) FROM msdb.dbo.sysjobactivity)
)
SELECT j.name,
  CAST(j.enabled AS INT),
  CASE WHEN lr.run_status IS NULL THEN -1 WHEN lr.run_status = 1 THEN 1 ELSE 0 END,
  ISNULL(DATEDIFF(SECOND, '19700101', msdb.dbo.agent_datetime(lr.run_date, lr.run_time)), 0),
  ISNULL((lr.run_duration / 10000) * 3600 + ((lr.run_duration / 100) % 100) * 60 + (lr.run_duration % 100), 0),
  CASE WHEN a.start_execution_date IS NOT NULL AND a.stop_execution_date IS NULL THEN 1 ELSE 0 END
FROM msdb.dbo.sysjobs j
LEFT JOIN last_run lr ON lr.job_id = j.job_id AND lr.rn = 1
LEFT JOIN activity a ON a.job_id = j.job_id
""",
            (rows, gauges) =>
            {
                foreach (var row in rows)
                {
                    var job = ("job", CollectorUtil.AsString(row[0]));
                    gauges.Set("mssql_agent_job_enabled", CollectorUtil.AsDouble(row[1]), job);
                    gauges.Set("mssql_agent_job_last_run_success", CollectorUtil.AsDouble(row[2]), job);
                    gauges.Set("mssql_agent_job_last_run_timestamp", CollectorUtil.AsDouble(row[3]), job);
                    gauges.Set("mssql_agent_job_last_duration_seconds", CollectorUtil.AsDouble(row[4]), job);
                    gauges.Set("mssql_agent_job_running", CollectorUtil.AsDouble(row[5]), job);
                }
            },
            Optional: true),
    ];
}
