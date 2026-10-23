using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

public class StartupTests
{
    [Fact]
    public void Server_WithoutConnectionString_RefusesToStartWithAClearMessage()
    {
        // Production: no user secrets, and no connection string in the committed settings.
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

        var error = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings:Finance", error.ToString());
    }
}
