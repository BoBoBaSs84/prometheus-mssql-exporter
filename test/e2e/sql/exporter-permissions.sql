-- Least-privilege login used by the exporter.
--
-- This is the script the e2e suite provisions and then scrapes with, so it is
-- kept in sync with the "Required permissions" section of the README. Replace
-- the __LOGIN__ / __PASSWORD__ placeholders before running it by hand.

USE master;
GO

CREATE LOGIN [__LOGIN__] WITH PASSWORD = '__PASSWORD__';
GO

-- Base rights: cover every always-on DMV collector.
GRANT VIEW ANY DEFINITION TO [__LOGIN__];
GRANT VIEW SERVER STATE TO [__LOGIN__];
GO

-- The backup, suspect-page and SQL Agent collectors read msdb.
USE msdb;
GO

CREATE USER [__LOGIN__] FOR LOGIN [__LOGIN__];
ALTER ROLE [db_datareader] ADD MEMBER [__LOGIN__];          -- backupset, suspect_pages, sysjobhistory, ...
ALTER ROLE [SQLAgentReaderRole] ADD MEMBER [__LOGIN__];     -- sysjobs / sysjobactivity
GRANT EXECUTE ON [dbo].[agent_datetime] TO [__LOGIN__];     -- called by the mssql_agent_jobs query
GO
