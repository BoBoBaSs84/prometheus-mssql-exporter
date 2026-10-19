using Microsoft.Data.SqlClient;

namespace MssqlExporter;

/// <summary>
/// Exporter configuration, built and validated from environment variables.
/// </summary>
public sealed record ExporterOptions
{
    public required string Server { get; init; }
    public required string User { get; init; }
    public required string Password { get; init; }
    public int Port { get; init; } = 1433;
    public bool Encrypt { get; init; } = true;
    public bool TrustServerCertificate { get; init; } = true;
    public int ConnectTimeoutMs { get; init; } = 15000;
    public int ExposePort { get; init; } = 4000;
    public bool CollectDefaultMetrics { get; init; } = true;
    public int QueryTimeoutMs { get; init; } = 30000;
    public bool ProbeEnabled { get; init; } = true;

    private static readonly string[] RequiredVariables = ["MSSQL_SERVER", "MSSQL_USER", "MSSQL_PASSWORD"];

    /// <summary>Loads options from the process environment.</summary>
    public static ExporterOptions FromEnvironment() => From(Environment.GetEnvironmentVariable);

    /// <summary>Loads options from an arbitrary variable lookup (used by tests).</summary>
    public static ExporterOptions From(Func<string, string?> env)
    {
        string[] missing = [.. RequiredVariables.Where(key => string.IsNullOrEmpty(env(key)))];
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Missing required environment variable(s): {string.Join(", ", missing)}");
        }

        return new ExporterOptions
        {
            Server = env("MSSQL_SERVER")!,
            User = env("MSSQL_USER")!,
            Password = env("MSSQL_PASSWORD")!,
            Port = ParseInt(env("MSSQL_PORT"), 1433),
            Encrypt = ParseBool(env("ENCRYPT"), true),
            TrustServerCertificate = ParseBool(env("TRUST_SERVER_CERTIFICATE"), true),
            ConnectTimeoutMs = ParseInt(env("CONNECT_TIMEOUT_MS"), 15000),
            ExposePort = ParseInt(env("EXPOSE"), 4000),
            CollectDefaultMetrics = ParseBool(env("COLLECT_DEFAULT_METRICS"), true),
            QueryTimeoutMs = ParseInt(env("QUERY_TIMEOUT_MS"), 30000),
            ProbeEnabled = ParseBool(env("PROBE_ENABLED"), true),
        };
    }

    /// <summary>
    /// Builds the connection string, optionally overriding host and port (used by <c>/probe</c>).
    /// SqlClient takes whole seconds, so the connect timeout is rounded up.
    /// </summary>
    public string BuildConnectionString(string? server = null, int? port = null)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{server ?? Server},{port ?? Port}",
            UserID = User,
            Password = Password,
            Encrypt = Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = TrustServerCertificate,
            ConnectTimeout = (int)Math.Ceiling(ConnectTimeoutMs / 1000.0),
            Pooling = false,
        };
        return builder.ConnectionString;
    }

    // Mirrors the Node parseBool: only "true" / "1" are true, empty falls back.
    internal static bool ParseBool(string? value, bool fallback) =>
        string.IsNullOrEmpty(value) ? fallback : value is "true" or "1";

    // Mirrors the Node parseIntOr / parseInt(value, 10): leading integer wins ("12ab" -> 12), garbage falls back.
    internal static int ParseInt(string? value, int fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var s = value.AsSpan().Trim();
        var end = 0;
        if (end < s.Length && (s[end] is '-' or '+'))
        {
            end++;
        }
        var digitsStart = end;
        while (end < s.Length && char.IsAsciiDigit(s[end]))
        {
            end++;
        }
        return end > digitsStart && int.TryParse(s[..end], out var parsed) ? parsed : fallback;
    }
}
