namespace MssqlExporter.Collectors;

/// <summary>The ordered list of every collector.</summary>
public static class Collectors
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        .. Instance.All,
        .. Connections.All,
        .. Errors.All,
        .. Databases.All,
        .. Io.All,
        .. Throughput.All,
        .. Memory.All,
        .. Activity.All,
        .. Waits.All,
        .. Space.All,
        .. Settings.All,
        .. Backups.All,
        .. Availability.All,
        .. AlwaysOn.All,
        .. Agent.All,
    ];

    /// <summary>The liveness collector, also used to answer a failed scrape with <c>mssql_up 0</c>.</summary>
    public static Collector Up { get; } = All.Single(c => c.Name == "mssql_up");
}
