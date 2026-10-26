using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

[Collection(SqlServerCollection.Name)]
public class FinancePlanReaderTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth May = new(2026, 5);

    private readonly RecurringLine _rent = Expense("Loyer", 650m);
    private readonly RecurringLine _endedInMarch = Expense("Salle de sport", 30m, end: new DateOnly(2026, 3, 31));
    // The two inclusive bounds: a line ending on the first day of the month, one starting on its last day.
    private readonly RecurringLine _endingOnAprilFirst = Expense("Piscine", 30m, day: 1, end: new DateOnly(2026, 4, 1));
    private readonly RecurringLine _startingOnAprilLast = Expense("Presse", 9.99m, day: 30, start: new DateOnly(2026, 4, 30));
    private readonly RecurringLine _startingInMay = Expense("Streaming", 13.49m, start: new DateOnly(2026, 5, 1));
    private readonly Budget _groceries = NewBudget("Courses", 200m);

    private RecurringLine[] AllLines => [_rent, _endedInMarch, _endingOnAprilFirst, _startingOnAprilLast, _startingInMay];

    [Fact]
    public async Task LoadPlan_HoldsEveryLine_EndedAndFutureOnesIncluded_AndEveryBudget()
    {
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);

        var plan = await new FinancePlanReader(context).LoadPlanAsync(Token);

        // Seen through the rules of the plan: every line exists, and its name is taken.
        Assert.All(AllLines, line => plan.Lines.EnsureExists(line.Id));
        Assert.Throws<NameTakenException>(() => plan.Lines.Create(Expense("salle de sport", 30m)));
        Assert.Throws<NameTakenException>(() => plan.Lines.Create(Expense("STREAMING", 30m)));
        plan.Budgets.EnsureExists(_groceries.Id);
    }

    [Fact]
    public async Task LoadLinesFrom_LeavesOutOnlyTheLinesEndedBeforeTheMonth()
    {
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);

        var lines = await new FinancePlanReader(context).LoadLinesFromAsync(April, Token);

        // Ending on April 1st still runs in April; starting in May is after it: both in sight.
        Assert.Equal(["Loyer", "Piscine", "Presse", "Streaming"], lines.Select(l => l.Name).Order());
    }

    [Fact]
    public async Task LoadMonth_ReturnsTheLinesActiveDuringTheMonth()
    {
        var month = await LoadAprilAsync();

        Assert.Equal(["Loyer", "Piscine", "Presse"], month.Lines.Select(l => l.Name).Order());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task LoadMonth_FiltersTheLinesLikeTheDomain(int monthNumber)
    {
        // The SQL filter duplicates RecurringLine.IsActiveIn: both must keep exactly the same lines.
        var yearMonth = new YearMonth(2026, monthNumber);
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);

        var month = await new FinancePlanReader(context).LoadMonthAsync(yearMonth, Token);

        Assert.Equal(AllLines.Where(l => l.IsActiveIn(yearMonth)).Select(l => l.Id).Order(), month.Lines.Select(l => l.Id).Order());
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
        Assert.Equal(670m + 30m + 9.99m, month.ActualSummary().CashRecurringExpenses);
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
        context.RecurringLines.AddRange(AllLines.Select(l => l.ToRow()));
        context.Budgets.Add(_groceries.ToRow());
        context.MonthPlans.Add(new MonthPlan(April, 150m).ToRow());
        context.BudgetActuals.AddRange(new BudgetActual(_groceries.Id, April, 235m).ToRow(), new BudgetActual(_groceries.Id, May, 999m).ToRow());
        context.RecurringOverrides.AddRange(
            RecurringOverride.WithAmount(_rent.Id, April, 670m).ToRow(),
            RecurringOverride.Skipped(_rent.Id, May).ToRow());
        await context.SaveChangesAsync(Token);
        return options;
    }
}
