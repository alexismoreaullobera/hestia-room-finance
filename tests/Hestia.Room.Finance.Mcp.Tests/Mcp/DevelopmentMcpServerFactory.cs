using Hestia.Room.Finance.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// In-memory MCP server running explicitly in Development: the /mcp tests rely on
/// appsettings.Development.json (AllowedHosts=localhost, dev CORS origins), and on the development startup
/// (migrations and demo plan) against the given test database.
/// </summary>
public sealed class DevelopmentMcpServerFactory(string connectionString) : WebApplicationFactory<Program>
{
    /// <summary>The month the server believes it is: the demo is seeded for it, and the tools default to it.</summary>
    // Not the real month on purpose: a server ignoring the injected clock would be caught.
    public static readonly YearMonth CurrentMonth = new(2027, 3);

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseEnvironment("Development")
        // Takes precedence over the user secrets of the developer machine: the tests never touch the local database.
        .UseSetting("ConnectionStrings:Finance", connectionString)
        // A fixed clock, mid-March 2027 in Paris: the expected figures do not depend on the day the tests run.
        .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(
            new FakeTimeProvider(new DateTimeOffset(2027, 3, 15, 10, 0, 0, TimeSpan.Zero))));
}
