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
    // A random password per run, instead of the well-known default one of Testcontainers, while the port is open.
    private readonly MsSqlContainer _container =
        new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04")
            .WithPassword($"Aa1!{Guid.NewGuid():N}")
            .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync(TestContext.Current.CancellationToken);

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    /// <summary>The connection string of a new database, not created yet: EF Core creates it when migrating.</summary>
    public string NewDatabaseConnectionString() =>
        new SqlConnectionStringBuilder(_container.GetConnectionString())
        {
            InitialCatalog = $"finance_test_{Guid.NewGuid():N}",
        }.ConnectionString;

    /// <summary>A new, empty database with every migration applied, and the options to connect to it.</summary>
    public async Task<DbContextOptions<FinanceDbContext>> CreateMigratedDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseRoomSqlServer(NewDatabaseConnectionString()).Options;

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
