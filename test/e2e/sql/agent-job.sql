-- A trivial SQL Agent job so the mssql_agent_jobs collector has something to
-- report, and so sysjobhistory carries the run_date/run_time that
-- msdb.dbo.agent_datetime() is called with.

-- Idempotent: the caller retries this script until the Agent stops reporting
-- "SQLServerAgent is starting".
IF EXISTS (SELECT 1 FROM msdb.dbo.sysjobs WHERE name = N'__JOB__')
  EXEC msdb.dbo.sp_delete_job @job_name = N'__JOB__';

EXEC msdb.dbo.sp_add_job
  @job_name = N'__JOB__',
  @enabled = 1,
  @description = N'prometheus-mssql-exporter e2e fixture';

EXEC msdb.dbo.sp_add_jobstep
  @job_name = N'__JOB__',
  @step_name = N'noop',
  @subsystem = N'TSQL',
  @command = N'SELECT 1;',
  @on_success_action = 1;

EXEC msdb.dbo.sp_add_jobserver
  @job_name = N'__JOB__',
  @server_name = N'(local)';
