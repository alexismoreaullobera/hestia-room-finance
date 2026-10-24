using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

[Collection(SqlServerCollection.Name)]
public class FinancePlanWriterTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth May = new(2026, 5);

    [Fact]
    public async Task SetStartingBalance_OnAMonthWithoutSettings_CreatesThem()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();

        await SetAsync(options, new MonthPlan(April, 150m));

        Assert.Equal(new MonthPlan(April, 150m), await LoadPlanAsync(options, April));
    }

    [Fact]
    public async Task SetStartingBalance_OnAMonthWithSettings_ReplacesTheBalance_InTheSameRow()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await SetAsync(options, new MonthPlan(April, 150m));

        // A new writer and context, as a second request would do.
        await SetAsync(options, new MonthPlan(April, -42.50m));

        Assert.Equal(new MonthPlan(April, -42.50m), await LoadPlanAsync(options, April));
        await using var context = new FinanceDbContext(options);
        Assert.Equal(1, await context.MonthPlans.CountAsync(Token));
    }

    [Fact]
    public async Task SetStartingBalance_LeavesTheOtherMonthsUntouched()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await SetAsync(options, new MonthPlan(May, 80m));

        await SetAsync(options, new MonthPlan(April, 150m));

        Assert.Equal(new MonthPlan(May, 80m), await LoadPlanAsync(options, May));
    }

    [Fact]
    public async Task SetStartingBalance_ToZero_IsSavedAsAChoice()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await SetAsync(options, new MonthPlan(April, 150m));

        await SetAsync(options, new MonthPlan(April, 0m));

        // A row with 0 €, not a deleted row: the reader would give 0 € either way, but the saved value is the user's.
        await using var context = new FinanceDbContext(options);
        var row = await context.MonthPlans.SingleAsync(Token);
        Assert.Equal(new MonthPlan(April, 0m), row.ToDomain());
    }

    [Fact]
    public async Task SetStartingBalance_AtTheBound_SurvivesTheDatabase()
    {
        // The largest balance the domain accepts fits decimal(18,2), negative included.
        var options = await sqlServer.CreateMigratedDatabaseAsync();

        await SetAsync(options, new MonthPlan(April, -999_999_999.99m));

        Assert.Equal(-999_999_999.99m, (await LoadPlanAsync(options, April)).StartingBalance);
    }

    private static async Task SetAsync(DbContextOptions<FinanceDbContext> options, MonthPlan plan)
    {
        await using var context = new FinanceDbContext(options);
        await new FinancePlanWriter(context).SetStartingBalanceAsync(plan, Token);
    }

    /// <summary>Read through the reader, with a new context: the value comes back from SQL Server, not from memory.</summary>
    private static async Task<MonthPlan> LoadPlanAsync(DbContextOptions<FinanceDbContext> options, YearMonth month)
    {
        await using var context = new FinanceDbContext(options);
        return (await new FinancePlanReader(context).LoadMonthAsync(month, Token)).Plan;
    }
}
