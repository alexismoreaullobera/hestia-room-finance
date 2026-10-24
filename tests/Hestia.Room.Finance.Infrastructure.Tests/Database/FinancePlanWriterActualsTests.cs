using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

/// <summary>
/// The actuals of a month through the writer, on a real SQL Server: budget actuals and line overrides, set, replaced
/// and cleared, each read back through the reader with a new context (the values come from SQL Server).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class FinancePlanWriterActualsTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth May = new(2026, 5);

    private readonly Budget _groceries = NewBudget("Courses", 320m);
    private readonly Budget _outings = NewBudget("Sorties", 120m);
    private readonly RecurringLine _rent = Expense("Loyer", 650m, day: 5);
    private readonly RecurringLine _water = Expense("Eau", 45m, day: 12);

    [Fact]
    public async Task SetBudgetActual_ThenAgain_KeepsTheLastAmount_InASingleRow()
    {
        var options = await SeedAsync();

        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 301.20m), Token));
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 347.85m), Token));

        Assert.Equal([new BudgetActual(_groceries.Id, April, 347.85m)], (await LoadAsync(options, April)).BudgetActuals);
    }

    [Fact]
    public async Task SetBudgetActual_ToZero_IsSaved()
    {
        // Nothing spent on that budget: an actual of 0 €, not "as planned".
        var options = await SeedAsync();

        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_outings.Id, April, 0m), Token));

        Assert.Equal([new BudgetActual(_outings.Id, April, 0m)], (await LoadAsync(options, April)).BudgetActuals);
    }

    [Fact]
    public async Task ClearBudgetActual_RemovesOnlyThatBudgetAndMonth_AndCanBeRepeated()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 301.20m), Token));
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_outings.Id, April, 96.50m), Token));
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, May, 280m), Token));

        await WriteAsync(options, w => w.ClearBudgetActualAsync(_groceries.Id, April, Token));
        await WriteAsync(options, w => w.ClearBudgetActualAsync(_groceries.Id, April, Token));

        Assert.Equal([new BudgetActual(_outings.Id, April, 96.50m)], (await LoadAsync(options, April)).BudgetActuals);
        Assert.Equal([new BudgetActual(_groceries.Id, May, 280m)], (await LoadAsync(options, May)).BudgetActuals);
    }

    [Fact]
    public async Task SetLineOverride_FromAnAmountToSkipped_ReplacesIt()
    {
        var options = await SeedAsync();

        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_water.Id, April, 52m), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(_water.Id, April), Token));

        Assert.Equal([RecurringOverride.Skipped(_water.Id, April)], (await LoadAsync(options, April)).Overrides);
    }

    [Fact]
    public async Task SetLineOverride_FromSkippedToAnAmount_ReplacesIt()
    {
        var options = await SeedAsync();

        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(_water.Id, April), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_water.Id, April, 52m), Token));

        Assert.Equal([RecurringOverride.WithAmount(_water.Id, April, 52m)], (await LoadAsync(options, April)).Overrides);
    }

    [Fact]
    public async Task ClearLineOverride_RemovesOnlyThatLineAndMonth_AndCanBeRepeated()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_water.Id, April, 52m), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_rent.Id, April, 670m), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(_water.Id, May), Token));

        await WriteAsync(options, w => w.ClearLineOverrideAsync(_water.Id, April, Token));
        await WriteAsync(options, w => w.ClearLineOverrideAsync(_water.Id, April, Token));

        Assert.Equal([RecurringOverride.WithAmount(_rent.Id, April, 670m)], (await LoadAsync(options, April)).Overrides);
        Assert.Equal([RecurringOverride.Skipped(_water.Id, May)], (await LoadAsync(options, May)).Overrides);
    }

    [Fact]
    public async Task ActualsAtTheBound_SurviveTheDatabase()
    {
        var options = await SeedAsync();

        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 999_999_999.99m), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_rent.Id, April, 999_999_999.99m), Token));

        var month = await LoadAsync(options, April);
        Assert.Equal(999_999_999.99m, Assert.Single(month.BudgetActuals).Amount);
        Assert.Equal(999_999_999.99m, Assert.Single(month.Overrides).ActualAmount);
    }

    [Fact]
    public async Task SetBudgetActual_WhenAnotherFirstSaveWinsTheInsert_EndsWithTheLastValue()
    {
        var options = await SeedAsync();
        var racing = new DbContextOptionsBuilder<FinanceDbContext>(options)
            .AddInterceptors(new FinancePlanWriterTests.OtherSaveFirst(options, new BudgetActual(_groceries.Id, April, 100m).ToRow()))
            .Options;

        await WriteAsync(racing, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 347.85m), Token));

        Assert.Equal([new BudgetActual(_groceries.Id, April, 347.85m)], (await LoadAsync(options, April)).BudgetActuals);
    }

    [Fact]
    public async Task SetLineOverride_WhenAnotherFirstSaveWinsTheInsert_EndsWithTheLastValue()
    {
        var options = await SeedAsync();
        var racing = new DbContextOptionsBuilder<FinanceDbContext>(options)
            .AddInterceptors(new FinancePlanWriterTests.OtherSaveFirst(options, RecurringOverride.WithAmount(_water.Id, April, 50m).ToRow()))
            .Options;

        await WriteAsync(racing, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(_water.Id, April), Token));

        Assert.Equal([RecurringOverride.Skipped(_water.Id, April)], (await LoadAsync(options, April)).Overrides);
    }

    [Fact]
    public async Task SetActuals_ForABudgetOrALineDeletedMeanwhile_FailWithTheForeignKey_NeverRetried()
    {
        // Deleted between the read of the month and the write: the database refuses (547), and the writer does not take
        // it for a race of two first saves (only a duplicate key is).
        var options = await SeedAsync();

        var budgetError = await Assert.ThrowsAsync<DbUpdateException>(() =>
            WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(Guid.CreateVersion7(), April, 10m), Token)));
        var lineError = await Assert.ThrowsAsync<DbUpdateException>(() =>
            WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(Guid.CreateVersion7(), April), Token)));

        Assert.Equal(547, Assert.IsType<SqlException>(budgetError.InnerException).Number);
        Assert.Equal(547, Assert.IsType<SqlException>(lineError.InnerException).Number);
    }

    private async Task<DbContextOptions<FinanceDbContext>> SeedAsync()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        context.Budgets.AddRange(_groceries.ToRow(), _outings.ToRow());
        context.RecurringLines.AddRange(_rent.ToRow(), _water.ToRow());
        await context.SaveChangesAsync(Token);
        return options;
    }

    /// <summary>One write, through a new writer and context, as one request would do.</summary>
    private static async Task WriteAsync(DbContextOptions<FinanceDbContext> options, Func<FinancePlanWriter, Task> write)
    {
        await using var context = new FinanceDbContext(options);
        await write(new FinancePlanWriter(context));
    }

    private static async Task<FinanceMonth> LoadAsync(DbContextOptions<FinanceDbContext> options, YearMonth month)
    {
        await using var context = new FinanceDbContext(options);
        return await new FinancePlanReader(context).LoadMonthAsync(month, Token);
    }
}
