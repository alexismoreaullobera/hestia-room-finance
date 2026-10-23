using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

public class StartupTests
{
    [Fact]
    public void Server_WithoutConnectionString_RefusesToStartWithAClearMessage()
    {
        // Production (no user secrets), and an empty connection string set by the test itself: a machine-level
        // ConnectionStrings__Finance environment variable cannot make the server start.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("ConnectionStrings:Finance", ""));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Finance", error.ToString());
    }
}
