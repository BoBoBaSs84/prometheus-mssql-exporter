using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace MssqlExporter.E2E.Tests;

/// <summary>Seeds the test instance: permission fixtures, the Agent job, and the waits they need.</summary>
internal static partial class Provisioner
{
    /// <summary><c>GO</c> is a client-side batch separator; SqlClient executes one batch per command.</summary>
    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BatchSeparator();

    /// <summary>
    /// Reads a fixture from <c>Sql/</c>, substitutes the <c>__NAME__</c> placeholders and runs it
    /// batch by batch on <paramref name="connection"/>.
    /// </summary>
    public static async Task RunScriptAsync(SqlConnection connection, string file, IReadOnlyDictionary<string, string>? replacements = null)
    {
        var sql = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Sql", file));
        foreach (var (placeholder, value) in replacements ?? new Dictionary<string, string>())
        {
            sql = sql.Replace(placeholder, value, StringComparison.Ordinal);
        }

        foreach (var batch in BatchSeparator().Split(sql))
        {
            if (!string.IsNullOrWhiteSpace(batch))
            {
                await ExecuteAsync(connection, batch);
            }
        }
    }

    public static async Task ExecuteAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<object?> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteScalarAsync();
    }

    /// <summary>Polls <paramref name="probe"/> until it returns true, or throws after <paramref name="attempts"/> tries.</summary>
    public static async Task WaitForAsync(string what, Func<Task<bool>> probe, int attempts = 60, int intervalMs = 2_000)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await probe())
            {
                return;
            }
            await Task.Delay(intervalMs);
        }
        throw new TimeoutException($"Timed out waiting for {what}");
    }

    /// <summary>
    /// Runs <paramref name="action"/> until it stops throwing. The msdb job procedures reject with
    /// "Cannot perform this operation while SQLServerAgent is starting" for a while after
    /// <c>sys.dm_server_services</c> already reports the service as running. The last error is rethrown.
    /// </summary>
    public static async Task RetryAsync(string what, Func<Task> action, int attempts = 60, int intervalMs = 2_000)
    {
        Exception? last = null;
        try
        {
            await WaitForAsync(
                what,
                async () =>
                {
                    try
                    {
                        await action();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        last = ex;
                        return false;
                    }
                },
                attempts,
                intervalMs);
        }
        catch (TimeoutException) when (last is not null)
        {
            throw last;
        }
    }

    /// <summary>The Agent service starts a while after the engine accepts connections.</summary>
    public static Task WaitForAgentAsync(SqlConnection connection) =>
        WaitForAsync(
            "the SQL Server Agent service to start",
            async () => await ScalarAsync(connection, "SELECT status_desc FROM sys.dm_server_services WHERE servicename LIKE 'SQL Server Agent%'") is "Running");

    /// <summary>
    /// Starts <paramref name="job"/> and waits for its outcome row (<c>step_id = 0</c>) to land in sysjobhistory —
    /// that row carries the run_date/run_time that the collector feeds to <c>msdb.dbo.agent_datetime()</c>.
    /// </summary>
    public static async Task RunJobOnceAsync(SqlConnection connection, string job)
    {
        await RetryAsync($"job {job} to start", () => ExecuteAsync(connection, $"EXEC msdb.dbo.sp_start_job @job_name = N'{job}'"));
        await WaitForAsync(
            $"job {job} to record a run",
            async () => Convert.ToInt32(await ScalarAsync(
                connection,
                $"""
                SELECT COUNT(*) FROM msdb.dbo.sysjobhistory h
                  JOIN msdb.dbo.sysjobs j ON j.job_id = h.job_id
                 WHERE j.name = N'{job}' AND h.step_id = 0
                """)) > 0);
    }
}
