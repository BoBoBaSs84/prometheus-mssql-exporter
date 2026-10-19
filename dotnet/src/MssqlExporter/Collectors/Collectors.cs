namespace MssqlExporter.Collectors;

/// <summary>The ordered list of every collector.</summary>
public static class Collectors
{
    public static IReadOnlyList<Collector> All { get; } =
    [
        .. Instance.All,
        .. Connections.All,
        .. Throughput.All,
    ];
}
