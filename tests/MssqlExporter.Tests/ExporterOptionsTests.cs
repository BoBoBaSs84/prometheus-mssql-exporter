using Microsoft.Data.SqlClient;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class ExporterOptionsTests
{
    private static Dictionary<string, string?> Base() => new()
    {
        ["MSSQL_SERVER"] = "db",
        ["MSSQL_USER"] = "sa",
        ["MSSQL_PASSWORD"] = "pw",
    };

    private static ExporterOptions Load(Dictionary<string, string?> env) => ExporterOptions.From(key => env.GetValueOrDefault(key));

    [TestMethod]
    public void ListsEveryMissingRequiredVariable()
    {
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => Load([]));
        StringAssert.Contains(ex.Message, "MSSQL_SERVER, MSSQL_USER, MSSQL_PASSWORD");
    }

    [TestMethod]
    public void ReportsASingleMissingVariable()
    {
        var env = Base();
        env.Remove("MSSQL_PASSWORD");
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => Load(env));
        StringAssert.Contains(ex.Message, "MSSQL_PASSWORD");
        Assert.DoesNotContain("MSSQL_USER", ex.Message);
    }

    [TestMethod]
    public void TreatsEmptyRequiredVariableAsMissing()
    {
        var env = Base();
        env["MSSQL_USER"] = "";
        var ex = Assert.ThrowsExactly<InvalidOperationException>(() => Load(env));
        StringAssert.Contains(ex.Message, "MSSQL_USER");
    }

    [TestMethod]
    public void IgnoresLegacyWindowsUsernameVariable()
    {
        var env = Base();
        env.Remove("MSSQL_USER");
        env["USERNAME"] = "windows-user";
        Assert.ThrowsExactly<InvalidOperationException>(() => Load(env));
    }

    [TestMethod]
    public void AppliesDefaults()
    {
        var options = Load(Base());
        Assert.AreEqual(1433, options.Port);
        Assert.IsTrue(options.Encrypt);
        Assert.IsTrue(options.TrustServerCertificate);
        Assert.AreEqual(15000, options.ConnectTimeoutMs);
        Assert.AreEqual(4000, options.ExposePort);
        Assert.IsTrue(options.CollectDefaultMetrics);
        Assert.AreEqual(30000, options.QueryTimeoutMs);
        Assert.IsTrue(options.ProbeEnabled);
    }

    [TestMethod]
    public void ParsesOverrides()
    {
        var env = Base();
        env["MSSQL_PORT"] = "1444";
        env["EXPOSE"] = "9000";
        env["ENCRYPT"] = "false";
        env["TRUST_SERVER_CERTIFICATE"] = "0";
        env["COLLECT_DEFAULT_METRICS"] = "false";
        env["PROBE_ENABLED"] = "false";
        env["CONNECT_TIMEOUT_MS"] = "5000";
        env["QUERY_TIMEOUT_MS"] = "1500";

        var options = Load(env);
        Assert.AreEqual(1444, options.Port);
        Assert.AreEqual(9000, options.ExposePort);
        Assert.IsFalse(options.Encrypt);
        Assert.IsFalse(options.TrustServerCertificate);
        Assert.IsFalse(options.CollectDefaultMetrics);
        Assert.IsFalse(options.ProbeEnabled);
        Assert.AreEqual(5000, options.ConnectTimeoutMs);
        Assert.AreEqual(1500, options.QueryTimeoutMs);
    }

    [TestMethod]
    public void FallsBackToDefaultWhenNumericVariableIsNotANumber()
    {
        var env = Base();
        env["MSSQL_PORT"] = "nope";
        Assert.AreEqual(1433, Load(env).Port);
    }

    [TestMethod]
    [DataRow("true", false, true)]
    [DataRow("1", false, true)]
    [DataRow("false", true, false)]
    [DataRow("0", true, false)]
    [DataRow("yes", true, false)]
    [DataRow("", true, true)]
    [DataRow(null, false, false)]
    public void ParseBoolMatchesNodeSemantics(string? value, bool fallback, bool expected) =>
        Assert.AreEqual(expected, ExporterOptions.ParseBool(value, fallback));

    [TestMethod]
    [DataRow("1444", 1444)]
    [DataRow("12ab", 12)]
    [DataRow(" 7", 7)]
    [DataRow("-3", -3)]
    [DataRow("nope", 99)]
    [DataRow("", 99)]
    [DataRow(null, 99)]
    public void ParseIntMatchesNodeParseInt(string? value, int expected) =>
        Assert.AreEqual(expected, ExporterOptions.ParseInt(value, 99));

    [TestMethod]
    public void BuildsConnectionString()
    {
        var env = Base();
        env["MSSQL_PORT"] = "1444";
        env["CONNECT_TIMEOUT_MS"] = "1500";
        env["ENCRYPT"] = "false";

        var builder = new SqlConnectionStringBuilder(Load(env).BuildConnectionString());
        Assert.AreEqual("db,1444", builder.DataSource);
        Assert.AreEqual("sa", builder.UserID);
        Assert.AreEqual("pw", builder.Password);
        Assert.AreEqual(2, builder.ConnectTimeout);
        Assert.IsTrue(builder.TrustServerCertificate);
        Assert.AreEqual(SqlConnectionEncryptOption.Optional, builder.Encrypt);
        Assert.IsFalse(builder.Pooling);
    }

    [TestMethod]
    public void ConnectionStringHonoursHostAndPortOverride()
    {
        var builder = new SqlConnectionStringBuilder(Load(Base()).BuildConnectionString("other", 1500));
        Assert.AreEqual("other,1500", builder.DataSource);
    }
}
