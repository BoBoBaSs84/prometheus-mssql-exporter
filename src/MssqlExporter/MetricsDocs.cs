using System.Text.RegularExpressions;

namespace MssqlExporter;

/// <summary>
/// Prints every collector's gauges and SQL for DBAs (<c>--print-metrics</c>); same output as the Node <c>npm run metrics</c>.
/// </summary>
public static partial class MetricsDocs
{
    [GeneratedRegex(@";\s*$")]
    private static partial Regex TrailingSemicolon();

    public static void Write(TextWriter writer)
    {
        void Line(string text) => writer.Write(text + "\n");
        var collectors = Collectors.Collectors.All;

        foreach (var collector in collectors)
        {
            // Mirrors console.log("--[", name, optional ? "(optional)" : "", "]"), which keeps the empty argument's spaces.
            Line($"--[ {collector.Name} {(collector.Optional ? "(optional)" : "")} ]");
            foreach (var (key, spec) in collector.Gauges)
            {
                Line($"-- {spec.Name ?? key} {spec.Help}");
            }
            Line(TrailingSemicolon().Replace(collector.Query.Trim(), "") + ";");
            Line("");
        }

        Line("/*");
        foreach (var collector in collectors)
        {
            foreach (var (key, spec) in collector.Gauges)
            {
                var labels = spec.LabelNames is { Count: > 0 } ? "{" + string.Join(",", spec.LabelNames) + "}" : "";
                Line($"- {spec.Name ?? key}{labels} {spec.Help}");
            }
        }
        Line("*/");
    }
}
