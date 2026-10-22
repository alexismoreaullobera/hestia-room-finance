using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

[Collection(SqlServerCollection.Name)]
public class FinancePlanReaderTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth April = new(2026, 4);
    private static readonly YearMonth May = new(2026, 5);
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private readonly RecurringLine _rent = Line("Loyer", start: new DateOnly(2025, 1, 1));
    private readonly RecurringLine _gymEndedInMarch = Line("Salle de sport", start: new DateOnly(2025, 1, 1), end: new DateOnly(2026, 3, 31));
    private readonly RecurringLine _gymEndingInApril = Line("Piscine", start: new DateOnly(2025, 1, 1), end: new DateOnly(2026, 4, 3));
    private readonly RecurringLine _startingInMay = Line("Streaming", start: new DateOnly(2026, 5, 1));
    private readonly Budget _groceries = new(Guid.CreateVersion7(), "Courses", 200m);

    [Fact]
    public async Task LoadMonth_ReturnsTheLinesActiveDuringTheMonth()
    {
        var month = await LoadAprilAsync();

        Assert.Equal(["Loyer", "Piscine"], month.Lines.Select(l => l.Name).Order());
    }

    [Fact]
    public async Task LoadMonth_ReturnsOnlyTheActualsAndOverridesOfTheMonth()
    {
        var month = await LoadAprilAsync();

        Assert.Equal([new BudgetActual(_groceries.Id, April, 235m)], month.BudgetActuals);
        Assert.Equal([RecurringOverride.WithAmount(_rent.Id, April, 670m)], month.Overrides);
    }

    [Fact]
    public async Task LoadMonth_ReturnsTheSavedStartingBalance()
    {
        Assert.Equal(new MonthPlan(April, 150m), (await LoadAprilAsync()).Plan);
    }

    [Fact]
    public async Task LoadMonth_WithoutSavedSettings_StartsFromZero()
    {
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);

        var may = await new FinancePlanReader(context).LoadMonthAsync(May, Token);

        Assert.Equal(new MonthPlan(May, 0m), may.Plan);
    }

    [Fact]
    public async Task LoadMonth_ReturnsAMonthReadyForTheCalculations()
    {
        var month = await LoadAprilAsync();

        // The rent override (670 € instead of 650 €) and the groceries actual (235 €) are taken into account.
        Assert.Equal(670m + 30m, month.ActualSummary().CashRecurringExpenses);
        Assert.Equal(235m, month.ActualSummary().BudgetsTotal);
        Assert.Equal(150m, month.Forecast().StartingBalance);
    }

    private async Task<FinanceMonth> LoadAprilAsync()
    {
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);
        return await new FinancePlanReader(context).LoadMonthAsync(April, Token);
    }

    private async Task<DbContextOptions<FinanceDbContext>> SeedAsync()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        context.RecurringLines.AddRange(new[] { _rent, _gymEndedInMarch, _gymEndingInApril, _startingInMay }.Select(l => l.ToRow()));
        context.Budgets.Add(_groceries.ToRow());
        context.MonthPlans.Add(new MonthPlan(April, 150m).ToRow());
        context.BudgetActuals.AddRange(new BudgetActual(_groceries.Id, April, 235m).ToRow(), new BudgetActual(_groceries.Id, May, 999m).ToRow());
        context.RecurringOverrides.AddRange(
            RecurringOverride.WithAmount(_rent.Id, April, 670m).ToRow(),
            RecurringOverride.Skipped(_rent.Id, May).ToRow());
        await context.SaveChangesAsync(Token);
        return options;
    }

    private static RecurringLine Line(string name, DateOnly start, DateOnly? end = null) =>
        new(Guid.CreateVersion7(), FlowKind.Expense, name, name == "Loyer" ? 650m : 30m, Frequency.Monthly, 2, start, endDate: end);
}
