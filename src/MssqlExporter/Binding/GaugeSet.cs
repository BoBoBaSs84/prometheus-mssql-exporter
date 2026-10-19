using Prometheus;

namespace MssqlExporter.Binding;

/// <summary>
/// The live gauges of one collector, addressed by spec key. Labels are passed as named pairs and
/// checked against the declared label names, because prometheus-net's own API is positional and a
/// swapped order would silently produce wrong series.
/// </summary>
public sealed class GaugeSet
{
    private readonly Dictionary<string, (Gauge Gauge, string[] LabelNames)> gauges = [];

    public GaugeSet(IReadOnlyDictionary<string, GaugeSpec> specs, MetricFactory factory)
    {
        foreach (var (key, spec) in specs)
        {
            string[] labelNames = [.. spec.LabelNames ?? []];
            var gauge = factory.CreateGauge(spec.Name ?? key, spec.Help, new GaugeConfiguration { LabelNames = labelNames });
            gauges.Add(key, (gauge, labelNames));
        }
    }

    public IEnumerable<string> Keys => gauges.Keys;

    /// <summary>Sets an unlabelled gauge. A <c>null</c> value (SQL NULL) leaves the gauge untouched.</summary>
    public void Set(string key, double? value) => Set(key, value, []);

    /// <summary>Sets one labelled series. A <c>null</c> value (SQL NULL) leaves the series untouched.</summary>
    public void Set(string key, double? value, params (string Name, string Value)[] labels)
    {
        var (gauge, labelNames) = Lookup(key);
        if (labels.Length != labelNames.Length)
        {
            throw LabelMismatch(key, labelNames, labels);
        }

        var ordered = new string[labelNames.Length];
        for (var i = 0; i < labelNames.Length; i++)
        {
            var match = Array.FindIndex(labels, l => l.Name == labelNames[i]);
            if (match < 0)
            {
                throw LabelMismatch(key, labelNames, labels);
            }
            ordered[i] = labels[match].Value;
        }

        if (value is null)
        {
            return;
        }

        if (ordered.Length == 0)
        {
            gauge.Set(value.Value);
        }
        else
        {
            gauge.WithLabels(ordered).Set(value.Value);
        }
    }

    private (Gauge Gauge, string[] LabelNames) Lookup(string key) =>
        gauges.TryGetValue(key, out var entry) ? entry : throw new KeyNotFoundException($"Unknown gauge key '{key}'");

    private static ArgumentException LabelMismatch(string key, string[] declared, (string Name, string Value)[] given) =>
        new($"Gauge '{key}' declares labels [{string.Join(", ", declared)}] but got [{string.Join(", ", given.Select(l => l.Name))}]");
}
