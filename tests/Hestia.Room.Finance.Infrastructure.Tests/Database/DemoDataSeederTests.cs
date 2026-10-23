using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Demo;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

[Collection(SqlServerCollection.Name)]
public class DemoDataSeederTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth CurrentMonth = new(2026, 10);

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
        Assert.Equal(demo.MonthPlans.Count, await read.MonthPlans.CountAsync(Token));
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

    [Theory]
    [InlineData("line")]
    [InlineData("budget")]
    [InlineData("month plan")]
    public async Task SeedIfEmpty_WithAnyRealData_TouchesNothing(string realData)
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        switch (realData)
        {
            case "line":
                context.RecurringLines.Add(Expense("Mon vrai loyer", 700m).ToRow());
                break;
            case "budget":
                context.Budgets.Add(NewBudget("Mon vrai budget", 50m).ToRow());
                break;
            case "month plan":
                // Another month than the demo one: without the check, the demo would be added next to it.
                context.MonthPlans.Add(new MonthPlan(CurrentMonth.AddMonths(-1), 80m).ToRow());
                break;
        }
        await context.SaveChangesAsync(Token);

        Assert.False(await new DemoDataSeeder(context).SeedIfEmptyAsync(CurrentMonth, Token));
        var rows = await context.RecurringLines.CountAsync(Token)
            + await context.Budgets.CountAsync(Token)
            + await context.MonthPlans.CountAsync(Token);
        Assert.Equal(1, rows);
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

        // The saved starting balance is read back, not the 0 € default of a month without settings.
        Assert.Equal(DemoPlan.For(CurrentMonth).MonthPlans.Single(), month.Plan);
        Assert.True(month.Forecast().HasAlert);
        Assert.NotEmpty(month.Forecast().ContributingDebits);
    }
}
