using Hestia.Room.Finance.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// One SQL Server container and one in-memory MCP server for every /mcp test of the run. The server migrates its
/// database and seeds the demo plan at startup (Development); the tools only read, so the tests can share it.
/// </summary>
public sealed class McpServerFixture : IAsyncLifetime
{
    private readonly SqlServerFixture _sqlServer = new();

    public DevelopmentMcpServerFactory Server { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _sqlServer.InitializeAsync();
        Server = new DevelopmentMcpServerFactory(_sqlServer.NewDatabaseConnectionString());
        // Started here, not lazily by the first test: a failing startup (migrations, seed) fails the fixture once,
        // with its own error, instead of every test in turn.
        Server.StartServer();
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
