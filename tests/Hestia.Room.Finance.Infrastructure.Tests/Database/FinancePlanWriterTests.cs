using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
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

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public async Task SetStartingBalance_AtTheBound_SurvivesTheDatabase(int sign)
    {
        // The largest balance the domain accepts fits decimal(18,2), either way.
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        var balance = sign * 999_999_999.99m;

        await SetAsync(options, new MonthPlan(April, balance));

        Assert.Equal(balance, (await LoadPlanAsync(options, April)).StartingBalance);
    }

    [Fact]
    public async Task SetStartingBalance_WhenAnotherFirstSaveWinsTheInsert_EndsWithTheLastValue()
    {
        // The writer finds no settings for April, then another request inserts them before it saves (two hosts, or a
        // host retrying the idempotent input): its own insert hits the primary key. Made deterministic by inserting
        // the other save from an interceptor, right before the writer's save.
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        var racing = new DbContextOptionsBuilder<FinanceDbContext>(options)
            .AddInterceptors(new OtherSaveFirst(options, new MonthPlan(April, 150m).ToRow()))
            .Options;

        await SetAsync(racing, new MonthPlan(April, -42.50m));

        // The last save wins, in the single row of the month: no error, no second row.
        Assert.Equal(new MonthPlan(April, -42.50m), await LoadPlanAsync(options, April));
        await using var context = new FinanceDbContext(options);
        Assert.Equal(1, await context.MonthPlans.CountAsync(Token));
    }

    /// <summary>
    /// Saves another row through its own context, once, just before the first save of the intercepted context: the race
    /// of two first saves of the same key, made deterministic. Shared with the tests of the actuals.
    /// </summary>
    internal sealed class OtherSaveFirst(DbContextOptions<FinanceDbContext> options, object otherRow) : SaveChangesInterceptor
    {
        private bool _done;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!_done)
            {
                _done = true;
                await using var otherContext = new FinanceDbContext(options);
                otherContext.Add(otherRow);
                await otherContext.SaveChangesAsync(cancellationToken);
            }

            return result;
        }
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
