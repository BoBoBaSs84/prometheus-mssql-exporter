using MssqlExporter.Data;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class SqlRunnerTests
{
    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(-5, 0)]
    [DataRow(1, 1)]
    [DataRow(999, 1)]
    [DataRow(1000, 1)]
    [DataRow(1001, 2)]
    [DataRow(30000, 30)]
    public void CommandTimeoutRoundsUpToWholeSeconds(int ms, int expected) =>
        Assert.AreEqual(expected, SqlRunner.ToCommandTimeoutSeconds(ms));

    [TestMethod]
    public void NormalizeMapsDbNullToNull()
    {
        Assert.IsNull(SqlRunner.Normalize(DBNull.Value));
        Assert.AreEqual("master", SqlRunner.Normalize("master"));
        Assert.AreEqual(42L, SqlRunner.Normalize(42L));
    }
}
