namespace MssqlExporter.Tests;

[TestClass]
public sealed class ProductVersionTests
{
    [TestMethod]
    public void ParsesAFourPartVersion() =>
        Assert.AreEqual(new ProductVersion(15, 0, 2000, 5), ProductVersion.Parse("15.0.2000.5"));

    [TestMethod]
    [DataRow("16.0.1000.6", 16)]
    [DataRow("17.0.900.7", 17)]
    public void ExtractsTheMajor(string version, int major) => Assert.AreEqual(major, ProductVersion.Parse(version).Major);

    [TestMethod]
    [DataRow("")]
    [DataRow("15.0")]
    [DataRow("15.0.2000")]
    [DataRow("abc")]
    [DataRow("15.0.x.5")]
    public void RejectsMalformedStrings(string version)
    {
        var ex = Assert.ThrowsExactly<FormatException>(() => ProductVersion.Parse(version));
        StringAssert.Contains(ex.Message, "Invalid product version");
    }

    [TestMethod]
    public void RejectsNonStrings()
    {
        Assert.ThrowsExactly<FormatException>(() => ProductVersion.Parse(null));
        Assert.ThrowsExactly<FormatException>(() => ProductVersion.Parse(15));
        Assert.ThrowsExactly<FormatException>(() => ProductVersion.Parse(new object()));
    }
}
