namespace MssqlExporter.Data;

/// <summary>Opens connections to SQL Server. Injectable so the HTTP path can be tested without a database.</summary>
public interface ISqlRunner
{
    Task<ISqlSession> ConnectAsync(string connectionString, CancellationToken cancellationToken);
}

/// <summary>An open connection that runs one SQL statement at a time.</summary>
public interface ISqlSession : IAsyncDisposable
{
    /// <summary>
    /// Runs a single statement and materialises every row as <c>object?[]</c> in SELECT column order
    /// (SQL NULL becomes <c>null</c>).
    /// </summary>
    /// <param name="timeoutMs">Per-query timeout; 0 disables it. Rounded up to whole seconds.</param>
    Task<object?[][]> RunQueryAsync(string sql, int timeoutMs, CancellationToken cancellationToken);
}
