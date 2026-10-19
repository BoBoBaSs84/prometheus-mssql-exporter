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
}
