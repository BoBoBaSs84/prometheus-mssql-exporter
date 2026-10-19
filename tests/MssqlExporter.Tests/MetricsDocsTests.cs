namespace MssqlExporter.Tests;

[TestClass]
public sealed class MetricsDocsTests
{
    private static string Render()
    {
        using var writer = new StringWriter();
        MetricsDocs.Write(writer);
        return writer.ToString().ReplaceLineEndings("\n");
    }

    [TestMethod]
    public void PrintsEveryCollectorHeaderGaugeAndQuery()
    {
        var text = Render();

        StringAssert.StartsWith(text, "--[ mssql_up  ]\n-- mssql_up UP status\nSELECT 1;\n\n");
        StringAssert.Contains(text, "--[ mssql_hadr_replica (optional) ]\n");
        StringAssert.Contains(text, "-- mssql_deadlocks Number of lock requests per second");
        foreach (var collector in Collectors.Collectors.All)
        {
            StringAssert.Contains(text, $"--[ {collector.Name} ");
        }
    }

    [TestMethod]
    public void EndsWithTheGaugeIndexIncludingLabels()
    {
        var text = Render();

        StringAssert.Contains(text, "/*\n- mssql_up UP status\n");
        StringAssert.Contains(text, "- mssql_connections{database,state} Number of active connections\n");
        StringAssert.EndsWith(text, "*/\n");
    }
}
