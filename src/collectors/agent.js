import createDebug from "debug";

const log = createDebug("metrics");

/**
 * SQL Server Agent service and job outcomes. All optional: Agent may not be
 * running and there may be no jobs. Requires read access to msdb job tables
 * (SQLAgentReaderRole or SELECT on msdb.dbo.sysjobs / sysjobhistory / sysjobactivity).
 */
export default [
  {
    name: "mssql_agent_up",
    optional: true,
    gauges: {
      mssql_agent_up: { help: "1 if the SQL Server Agent service is running" },
    },
    query: `SELECT CASE WHEN status_desc = 'Running' THEN 1 ELSE 0 END
FROM sys.dm_server_services
WHERE servicename LIKE 'SQL Server Agent%'`,
    collect: (rows, metrics) => {
      log("Fetched agent service status", rows[0][0].value);
      metrics.mssql_agent_up.set(rows[0][0].value);
    },
  },
  {
    name: "mssql_agent_jobs",
    optional: true,
    gauges: {
      mssql_agent_job_enabled: { help: "1 if the job is enabled", labelNames: ["job"] },
      mssql_agent_job_last_run_success: { help: "Last outcome: 1=succeeded 0=failed/other -1=never run", labelNames: ["job"] },
      mssql_agent_job_last_run_timestamp: { help: "Unix timestamp of the last job run (0 = never)", labelNames: ["job"] },
      mssql_agent_job_last_duration_seconds: { help: "Duration of the last job run, in seconds", labelNames: ["job"] },
      mssql_agent_job_running: { help: "1 if the job is currently executing", labelNames: ["job"] },
    },
    query: `WITH last_run AS (
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
LEFT JOIN activity a ON a.job_id = j.job_id`,
    collect: (rows, metrics) => {
      log("Fetched agent jobs", rows.length);
      for (const row of rows) {
        const job = row[0].value;
        metrics.mssql_agent_job_enabled.set({ job }, row[1].value);
        metrics.mssql_agent_job_last_run_success.set({ job }, row[2].value);
        metrics.mssql_agent_job_last_run_timestamp.set({ job }, row[3].value);
        metrics.mssql_agent_job_last_duration_seconds.set({ job }, row[4].value);
        metrics.mssql_agent_job_running.set({ job }, row[5].value);
      }
    },
  },
];
