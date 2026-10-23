using Hestia.Room.Finance.Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Room.Finance.Infrastructure.Tests;

/// <summary>The registrations of the room, without any database: resolving the services never opens a connection.</summary>
public class FinanceInfrastructureTests
{
    // Validation on: a scoped service resolved from the root, or a missing dependency, throws.
    private static readonly ServiceProviderOptions Strict = new() { ValidateScopes = true, ValidateOnBuild = true };

    [Fact]
    public void AddFinanceInfrastructure_RegistersTheReaderAndTheSeeder()
    {
        using var provider = new ServiceCollection().AddFinanceInfrastructure("Server=offline").BuildServiceProvider(Strict);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<FinancePlanReader>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DemoDataSeeder>());
    }

    [Fact]
    public void AddFinanceInfrastructure_GivesOneContextPerScope()
    {
        using var provider = new ServiceCollection().AddFinanceInfrastructure("Server=offline").BuildServiceProvider(Strict);
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var context = first.ServiceProvider.GetRequiredService<FinanceDbContext>();

        Assert.Same(context, first.ServiceProvider.GetRequiredService<FinanceDbContext>());
        Assert.NotSame(context, second.ServiceProvider.GetRequiredService<FinanceDbContext>());
        // Scoped, never resolved from the root: a context outliving its request would be shared between requests.
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<FinanceDbContext>());
    }

    [Fact]
    public void AddFinanceInfrastructure_UsesTheRoomSqlServerSettings()
    {
        using var provider = new ServiceCollection().AddFinanceInfrastructure("Server=offline").BuildServiceProvider(Strict);
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();

        var sqlServer = RelationalOptionsExtension.Extract(context.GetService<IDbContextOptions>());

        Assert.Equal("Server=offline", sqlServer.ConnectionString);
        Assert.Equal(FinanceDbContext.Schema, sqlServer.MigrationsHistoryTableSchema);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void AddFinanceInfrastructure_WithoutConnectionString_Throws(string connectionString)
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddFinanceInfrastructure(connectionString));
    }
}
