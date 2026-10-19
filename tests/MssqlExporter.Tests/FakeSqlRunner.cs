using MssqlExporter.Data;

namespace MssqlExporter.Tests;

/// <summary>Scriptable <see cref="ISqlRunner"/> that records what the exporter asked of it.</summary>
internal sealed class FakeSqlRunner : ISqlRunner
{
    private readonly object gate = new();

    public Func<string, Exception?> ConnectFailure { get; set; } = _ => null;

    public Func<string, object?[][]> Query { get; set; } = sql => sql == "SELECT 1" ? [[1]] : [];

    public List<string> ConnectionStrings { get; } = [];

    public List<int> Timeouts { get; } = [];

    public int Disposed { get; private set; }

    public Task<ISqlSession> ConnectAsync(string connectionString, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            ConnectionStrings.Add(connectionString);
        }

        return ConnectFailure(connectionString) is { } failure
            ? Task.FromException<ISqlSession>(failure)
            : Task.FromResult<ISqlSession>(new Session(this));
    }

    private sealed class Session(FakeSqlRunner owner) : ISqlSession
    {
        public Task<object?[][]> RunQueryAsync(string sql, int timeoutMs, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (owner.gate)
            {
                owner.Timeouts.Add(timeoutMs);
            }
            return Task.FromResult(owner.Query(sql));
        }

        public ValueTask DisposeAsync()
        {
            lock (owner.gate)
            {
                owner.Disposed++;
            }
            return ValueTask.CompletedTask;
        }
    }
}
