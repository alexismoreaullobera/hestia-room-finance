using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

[Collection(SqlServerCollection.Name)]
public class DevelopmentDatabaseTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth CurrentMonth = new(2026, 10);

    [Fact]
    public async Task PrepareDevelopmentDatabase_OnANewDatabase_MigratesAndSeedsTheDemo()
    {
        await using var provider = Services(sqlServer.NewDatabaseConnectionString());

        Assert.True(await provider.PrepareDevelopmentDatabaseAsync(CurrentMonth, Token));

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Token));
        Assert.Equal(DemoPlan.For(CurrentMonth).Lines.Count, await context.RecurringLines.CountAsync(Token));
    }

    [Fact]
    public async Task PrepareDevelopmentDatabase_RunAtEveryStart_SeedsOnlyOnce()
    {
        await using var provider = Services(sqlServer.NewDatabaseConnectionString());
        await provider.PrepareDevelopmentDatabaseAsync(CurrentMonth, Token);

        // The next start: nothing left to migrate, and the plan is already there.
        Assert.False(await provider.PrepareDevelopmentDatabaseAsync(CurrentMonth, Token));

        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        Assert.Equal(DemoPlan.For(CurrentMonth).Lines.Count, await context.RecurringLines.CountAsync(Token));
    }

    private static ServiceProvider Services(string connectionString) =>
        new ServiceCollection().AddFinanceInfrastructure(connectionString)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
}
