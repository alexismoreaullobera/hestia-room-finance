namespace Hestia.Room.Finance.Domain.Tests;

public class MonthSummaryActualTests
{
    private static readonly YearMonth April = new(2026, 4);
    private static readonly YearMonth May = new(2026, 5);
    private static readonly DateOnly LongAgo = new(2020, 1, 1);

    private readonly RecurringLine _salary = Monthly(FlowKind.Income, "Salary", 2000m, day: 28);
    private readonly RecurringLine _rent = Monthly(FlowKind.Expense, "Rent", 650m, day: 5);
    private readonly RecurringLine _water = Monthly(FlowKind.Expense, "Water", 45m, day: 15);
    private readonly Budget _groceries = new(Guid.NewGuid(), "Groceries", 200m);

    [Fact]
    public void Actual_WithoutAnyActualOrOverride_EqualsThePlan()
    {
        var actual = Actual([], []);

        Assert.Equal(Planned(), actual);
    }

    [Fact]
    public void Actual_BudgetActual_ReplacesThePlannedAmount()
    {
        var actual = Actual([new BudgetActual(_groceries.Id, April, 235m)], []);

        Assert.Equal(235m, actual.Budgets);
        Assert.Equal(2000m - 695m - 235m, actual.SavingsCapacity);
    }

    [Fact]
    public void Actual_BudgetActualOfZero_MeansNothingWasSpent()
    {
        var actual = Actual([new BudgetActual(_groceries.Id, April, 0m)], []);

        Assert.Equal(0m, actual.Budgets);
    }

    [Fact]
    public void Actual_OverrideWithAmount_ReplacesTheAmountOfTheLine()
    {
        var actual = Actual([], [RecurringOverride.WithAmount(_water.Id, April, 52m)]);

        Assert.Equal(650m + 52m, actual.RecurringExpenses);
    }

    [Fact]
    public void Actual_SkippedLine_CountsForNothing()
    {
        var actual = Actual([], [RecurringOverride.Skipped(_salary.Id, April)]);

        Assert.Equal(0m, actual.Incomes);
    }

    [Fact]
    public void Actual_ActualsAndOverridesOfAnotherMonth_AreIgnored()
    {
        var actual = Actual(
            [new BudgetActual(_groceries.Id, May, 999m)],
            [RecurringOverride.WithAmount(_rent.Id, May, 999m)]);

        Assert.Equal(Planned(), actual);
    }

    [Fact]
    public void Actual_OverrideOfALineNotDueThisMonth_IsIgnored()
    {
        var insurance = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Car insurance", 360m, Frequency.Yearly, 10, LongAgo, anchorMonth: 3);

        var actual = MonthSummary.Actual(April, [insurance], [], [], [RecurringOverride.WithAmount(insurance.Id, April, 400m)]);

        Assert.Equal(0m, actual.RecurringExpenses);
    }

    [Fact]
    public void Actual_SmoothedReading_StaysThePlannedOne()
    {
        var actual = Actual([], [RecurringOverride.WithAmount(_water.Id, April, 52m)]);

        Assert.Equal(Planned().SmoothedRecurringExpenses, actual.SmoothedRecurringExpenses);
    }

    [Fact]
    public void Actual_TwoActualsForTheSameBudgetAndMonth_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => Actual(
            [new BudgetActual(_groceries.Id, April, 100m), new BudgetActual(_groceries.Id, April, 120m)], []));

        Assert.Equal("budgetActuals", error.ParamName);
    }

    [Fact]
    public void Actual_TwoOverridesForTheSameLineAndMonth_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => Actual([],
            [RecurringOverride.WithAmount(_water.Id, April, 50m), RecurringOverride.Skipped(_water.Id, April)]));

        Assert.Equal("overrides", error.ParamName);
    }

    [Fact]
    public void BudgetActual_WithANegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetActual(_groceries.Id, April, -1m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void RecurringOverride_WithAmountNotPositive_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RecurringOverride.WithAmount(_water.Id, April, amount));
    }

    [Fact]
    public void RecurringOverride_Skipped_HasNoAmount()
    {
        var skipped = RecurringOverride.Skipped(_water.Id, April);

        Assert.True(skipped.IsSkipped);
        Assert.Null(skipped.ActualAmount);
    }

    private MonthSummary Planned() => MonthSummary.Planned(April, [_salary, _rent, _water], [_groceries]);

    private MonthSummary Actual(BudgetActual[] budgetActuals, RecurringOverride[] overrides) =>
        MonthSummary.Actual(April, [_salary, _rent, _water], [_groceries], budgetActuals, overrides);

    private static RecurringLine Monthly(FlowKind kind, string name, decimal amount, int day) =>
        new(Guid.NewGuid(), kind, name, amount, Frequency.Monthly, day, LongAgo);
}
