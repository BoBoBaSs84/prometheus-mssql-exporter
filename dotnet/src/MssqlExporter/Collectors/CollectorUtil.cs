using System.Globalization;

namespace MssqlExporter.Collectors;

/// <summary>Shared helpers for collectors.</summary>
public static class CollectorUtil
{
    /// <summary>
    /// Converts a cell to a gauge value. SQL NULL (<c>null</c>) yields <c>null</c> so callers decide explicitly
    /// instead of silently reporting 0. Integer-looking strings are accepted, as the Node exporter coerced them.
    /// </summary>
    /// <exception cref="InvalidCastException">The cell is not numeric.</exception>
    public static double? AsDouble(object? value) => value switch
    {
        null => null,
        double d => d,
        float f => f,
        long l => l,
        int i => i,
        short s => s,
        byte b => b,
        decimal m => (double)m,
        bool flag => flag ? 1 : 0,
        string text when double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
        _ => throw new InvalidCastException($"Cannot convert {value.GetType().Name} '{value}' to a gauge value"),
    };

    /// <summary>Converts a cell to a label value; SQL NULL becomes the empty string.</summary>
    public static string AsString(object? value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

    /// <summary>
    /// Turns rows shaped <c>[name, value, ...]</c> into a trimmed-name lookup, for
    /// <c>sys.dm_os_performance_counters</c>-style "counter_name, cntr_value" queries.
    /// </summary>
    public static Dictionary<string, double> ByName(IEnumerable<object?[]> rows)
    {
        var map = new Dictionary<string, double>();
        foreach (var row in rows)
        {
            map[AsString(row[0]).Trim()] = AsDouble(row[1]) ?? 0;
        }
        return map;
    }

    /// <summary>Safe ratio as a percentage; 0 when the base counter is 0.</summary>
    public static double RatioPercent(double value, double @base) => @base > 0 ? value / @base * 100 : 0;
}
