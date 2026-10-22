using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

/// <summary>
/// One throwaway SQL Server container (Testcontainers) shared by every database test of the run: starting SQL Server
/// takes seconds, so it is done once. Each test then gets its own fresh database, migrated from scratch: tests never
/// see each other's data. Requires Docker (Docker Desktop locally, already there on the CI runners).
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    // Same pinned image as docker-compose.yml: the tests run against the version used locally.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04").Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>A new, empty database with every migration applied, and the options to connect to it.</summary>
    public async Task<DbContextOptions<FinanceDbContext>> CreateMigratedDatabaseAsync()
    {
        var connection = new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"finance_test_{Guid.NewGuid():N}",
        };
        var options = FinanceDbContext.UseRoomSqlServer(new DbContextOptionsBuilder<FinanceDbContext>(), connection.ConnectionString).Options;

        await using var context = new FinanceDbContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return options;
    }
}

/// <summary>Every class of this collection shares the same SQL Server container.</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
