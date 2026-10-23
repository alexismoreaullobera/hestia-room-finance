using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Time;
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
    /// <summary>
    /// The time the server believes it is: mid-March 2027 in Paris. Not the real month on purpose, so that a server
    /// ignoring the injected clock is caught; and fixed, so that the expected figures do not depend on the run day.
    /// </summary>
    public static readonly DateTimeOffset Now = new(2027, 3, 15, 10, 0, 0, TimeSpan.Zero);

    /// <summary>The month of <see cref="Now"/> in Paris: the demo is seeded for it, and the tools default to it.</summary>
    public static readonly YearMonth CurrentMonth = new FinanceClock(new FakeTimeProvider(Now)).CurrentMonth();

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder
        .UseEnvironment("Development")
        // Takes precedence over the user secrets of the developer machine: the tests never touch the local database.
        .UseSetting("ConnectionStrings:Finance", connectionString)
        .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(new FakeTimeProvider(Now)));
}
