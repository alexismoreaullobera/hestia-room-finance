using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// In-memory MCP server running explicitly in Development: the /mcp tests rely on
/// appsettings.Development.json (AllowedHosts=localhost, dev CORS origins).
/// </summary>
public sealed class DevelopmentMcpServerFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");
}
