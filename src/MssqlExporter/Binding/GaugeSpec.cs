namespace MssqlExporter.Binding;

/// <summary>Describes one gauge. <paramref name="Name"/> defaults to the dictionary key it is declared under.</summary>
public sealed record GaugeSpec(string Help, string? Name = null, IReadOnlyList<string>? LabelNames = null);
