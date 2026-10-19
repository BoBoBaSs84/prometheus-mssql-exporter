using Microsoft.Data.SqlClient;

namespace MssqlExporter.Data;

public sealed class SqlRunner(ILogger<SqlRunner> logger) : ISqlRunner
{
    public async Task<ISqlSession> ConnectAsync(string connectionString, CancellationToken cancellationToken)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        logger.LogDebug(
            "Connecting to {User}@{Server} (encrypt: {Encrypt}, trustServerCertificate: {Trust})",
            builder.UserID,
            builder.DataSource,
            builder.Encrypt,
            builder.TrustServerCertificate);

        var connection = new SqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        logger.LogDebug("Connected");
        return new Session(connection, logger);
    }

    /// <summary>SqlClient's <c>CommandTimeout</c> is whole seconds; sub-second values round up, 0 stays "no timeout".</summary>
    internal static int ToCommandTimeoutSeconds(int timeoutMs) => timeoutMs <= 0 ? 0 : (int)Math.Ceiling(timeoutMs / 1000.0);

    internal static object? Normalize(object value) => value is DBNull ? null : value;

    private sealed class Session(SqlConnection connection, ILogger logger) : ISqlSession
    {
        public async Task<object?[][]> RunQueryAsync(string sql, int timeoutMs, CancellationToken cancellationToken)
        {
            await using var command = new SqlCommand(sql, connection) { CommandTimeout = ToCommandTimeoutSeconds(timeoutMs) };
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var rows = new List<object?[]>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new object?[reader.FieldCount];
                for (var i = 0; i < row.Length; i++)
                {
                    row[i] = Normalize(reader.GetValue(i));
                }
                rows.Add(row);
            }
            return [.. rows];
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            logger.LogDebug("Connection closed");
        }
    }
}
