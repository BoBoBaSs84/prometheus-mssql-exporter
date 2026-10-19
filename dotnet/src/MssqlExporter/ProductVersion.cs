using System.Globalization;
using System.Text.RegularExpressions;

namespace MssqlExporter;

/// <summary>A four-part SQL Server product version, e.g. <c>15.0.2000.5</c>.</summary>
public readonly partial record struct ProductVersion(int Major, int Minor, int Patch, int Build)
{
    [GeneratedRegex(@"^([0-9]+)\.([0-9]+)\.([0-9]+)\.([0-9]+)$")]
    private static partial Regex Pattern();

    /// <summary>Parses <c>SERVERPROPERTY('productversion')</c>.</summary>
    /// <exception cref="FormatException">The value is not a four-part numeric version.</exception>
    public static ProductVersion Parse(object? value)
    {
        if (value is not string text || text.Length == 0)
        {
            throw new FormatException($"Invalid product version {Describe(value)}");
        }

        var match = Pattern().Match(text);
        if (!match.Success)
        {
            throw new FormatException($"Invalid product version {text}");
        }

        int Part(int group) => int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);
        return new ProductVersion(Part(1), Part(2), Part(3), Part(4));
    }

    private static string Describe(object? value) => value is null ? "null" : $"{value} ({value.GetType().Name})";
}
