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
    }

    public async ValueTask DisposeAsync()
    {
        await Server.DisposeAsync();
        await _sqlServer.DisposeAsync();
    }
}

/// <summary>Every class of this collection shares the same MCP server and database.</summary>
[CollectionDefinition(Name)]
public sealed class McpServerCollection : ICollectionFixture<McpServerFixture>
{
    public const string Name = "MCP server";
}
