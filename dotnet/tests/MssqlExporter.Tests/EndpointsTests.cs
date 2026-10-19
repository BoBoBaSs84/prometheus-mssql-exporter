using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MssqlExporter.Tests;

[TestClass]
public sealed class EndpointsTests
{
    private static WebApplicationFactory<Program> factory = null!;

    [ClassInitialize]
    public static void Init(TestContext _)
    {
        Environment.SetEnvironmentVariable("MSSQL_SERVER", "db");
        Environment.SetEnvironmentVariable("MSSQL_USER", "sa");
        Environment.SetEnvironmentVariable("MSSQL_PASSWORD", "pw");
        factory = new WebApplicationFactory<Program>();
    }

    [ClassCleanup]
    public static void Cleanup() => factory.Dispose();

    [TestMethod]
    public async Task HealthzReportsOk()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/healthz");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        StringAssert.Contains(await response.Content.ReadAsStringAsync(), "\"status\":\"ok\"");
    }

    [TestMethod]
    public async Task RootRedirectsToMetrics()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var response = await client.GetAsync("/");

        Assert.AreEqual(HttpStatusCode.Redirect, response.StatusCode);
        Assert.AreEqual("/metrics", response.Headers.Location?.OriginalString);
    }
}
