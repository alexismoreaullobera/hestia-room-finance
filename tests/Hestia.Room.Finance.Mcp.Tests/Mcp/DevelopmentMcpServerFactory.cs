using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// In-memory MCP server running explicitly in Development: the /mcp tests rely on
/// appsettings.Development.json (AllowedHosts=localhost, dev CORS origins), and on the development startup
/// (migrations and demo plan) against the given test database.
/// </summary>
public sealed class DevelopmentMcpServerFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseEnvironment("Development")
        // Takes precedence over the user secrets of the developer machine: the tests never touch the local database.
        .UseSetting("ConnectionStrings:Finance", connectionString);
}
