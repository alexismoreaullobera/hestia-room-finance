using Hestia.Room.Finance.Infrastructure;
using Hestia.Room.Finance.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// One SQL Server container and one in-memory MCP server for every /mcp test of the run. The server migrates its
/// database and seeds the demo plan at startup (Development). The tests share it: the input tests write only to months
/// years after the demo plan, and clear them after each test (<see cref="WrittenRows.DeleteTestWritesAsync"/>).
/// </summary>
public sealed class McpServerFixture : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlServer = new();

    public DevelopmentMcpServerFactory Server { get; private set; } = null!;

    /// <summary>The budgets of the demo seeded at startup: the only ones the cleanup of the input tests keeps.</summary>
    public IReadOnlyList<Guid> DemoBudgetIds { get; private set; } = [];

    /// <summary>The recurring lines of the demo, as for the budgets.</summary>
    public IReadOnlyList<Guid> DemoLineIds { get; private set; } = [];

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.InitializeAsync();
        Server = new DevelopmentMcpServerFactory(_sqlServer.NewDatabaseConnectionString());
        // Started here, not lazily by the first test: a failing startup (migrations, seed) fails the fixture once,
        // with its own error, instead of every test in turn.
        Server.StartServer();

        await using var scope = Server.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        DemoBudgetIds = await context.Budgets.Select(b => b.Id).ToListAsync();
        DemoLineIds = await context.RecurringLines.Select(l => l.Id).ToListAsync();
    }

    public async ValueTask DisposeAsync()
    {
        // Null when the container did not start (no Docker): the original error stays visible.
        if (Server is not null)
        {
            await Server.DisposeAsync();
        }

        await _sqlServer.DisposeAsync();
    }
}

/// <summary>Every class of this collection shares the same MCP server and database.</summary>
[CollectionDefinition(Name)]
public sealed class McpServerCollection : ICollectionFixture<McpServerFixture>
{
    public const string Name = "MCP server";
}
