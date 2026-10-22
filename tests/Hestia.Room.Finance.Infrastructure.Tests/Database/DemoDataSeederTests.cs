using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Demo;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

[Collection(SqlServerCollection.Name)]
public class DemoDataSeederTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth CurrentMonth = new(2026, 10);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SeedIfEmpty_OnAnEmptyDatabase_SavesTheWholeDemoPlan()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        var demo = DemoPlan.For(CurrentMonth);

        await using (var context = new FinanceDbContext(options))
        {
            Assert.True(await new DemoDataSeeder(context).SeedIfEmptyAsync(CurrentMonth, Token));
        }

        await using var read = new FinanceDbContext(options);
        Assert.Equal(demo.Lines.Count, await read.RecurringLines.CountAsync(Token));
        Assert.Equal(demo.Budgets.Count, await read.Budgets.CountAsync(Token));
        Assert.Equal(demo.BudgetActuals.Count, await read.BudgetActuals.CountAsync(Token));
        Assert.Equal(demo.Overrides.Count, await read.RecurringOverrides.CountAsync(Token));
    }

    [Fact]
    public async Task SeedIfEmpty_RunTwice_SeedsOnlyOnce()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using (var first = new FinanceDbContext(options))
        {
            await new DemoDataSeeder(first).SeedIfEmptyAsync(CurrentMonth, Token);
        }

        await using var second = new FinanceDbContext(options);

        Assert.False(await new DemoDataSeeder(second).SeedIfEmptyAsync(CurrentMonth, Token));
        Assert.Equal(DemoPlan.For(CurrentMonth).Lines.Count, await second.RecurringLines.CountAsync(Token));
    }

    [Fact]
    public async Task SeedIfEmpty_WithARealBudget_TouchesNothing()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        context.Budgets.Add(new Budget(Guid.CreateVersion7(), "Mon vrai budget", 50m).ToRow());
        await context.SaveChangesAsync(Token);

        Assert.False(await new DemoDataSeeder(context).SeedIfEmptyAsync(CurrentMonth, Token));
        Assert.Equal(1, await context.Budgets.CountAsync(Token));
        Assert.False(await context.RecurringLines.AnyAsync(Token));
    }

    [Fact]
    public async Task SeededDemo_ReadBackFromTheDatabase_ShowsTheAlertOfTheCurrentMonth()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using (var seed = new FinanceDbContext(options))
        {
            await new DemoDataSeeder(seed).SeedIfEmptyAsync(CurrentMonth, Token);
        }

        await using var read = new FinanceDbContext(options);
        var month = await new FinancePlanReader(read).LoadMonthAsync(CurrentMonth, Token);

        Assert.True(month.Forecast().HasAlert);
        Assert.NotEmpty(month.Forecast().ContributingDebits);
    }
}
