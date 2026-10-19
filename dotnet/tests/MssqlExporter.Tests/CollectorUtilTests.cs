using MssqlExporter.Collectors;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class CollectorUtilTests
{
    [TestMethod]
    public void AsDoubleConvertsIntegralAndFloatingTypes()
    {
        Assert.AreEqual(10d, CollectorUtil.AsDouble(10L));
        Assert.AreEqual(20d, CollectorUtil.AsDouble(20));
        Assert.AreEqual(3d, CollectorUtil.AsDouble((short)3));
        Assert.AreEqual(4d, CollectorUtil.AsDouble((byte)4));
        Assert.AreEqual(1.5d, CollectorUtil.AsDouble(1.5m));
        Assert.AreEqual(2.5d, CollectorUtil.AsDouble(2.5f));
        Assert.AreEqual(1d, CollectorUtil.AsDouble(true));
        Assert.AreEqual(0d, CollectorUtil.AsDouble(false));
    }

    [TestMethod]
    public void AsDoubleParsesIntegerStrings()
    {
        Assert.AreEqual(4352d, CollectorUtil.AsDouble("4352"));
        Assert.AreEqual(-7d, CollectorUtil.AsDouble("-7"));
        Assert.AreEqual(0d, CollectorUtil.AsDouble("0"));
    }

    [TestMethod]
    public void AsDoubleReturnsNullForNull() => Assert.IsNull(CollectorUtil.AsDouble(null));

    [TestMethod]
    [DataRow("master")]
    [DataRow("/var/opt/mssql/data/master.mdf")]
    [DataRow("16.0.4225.1")]
    public void AsDoubleRejectsNonNumericStrings(string text) =>
        Assert.ThrowsExactly<InvalidCastException>(() => CollectorUtil.AsDouble(text));
}
