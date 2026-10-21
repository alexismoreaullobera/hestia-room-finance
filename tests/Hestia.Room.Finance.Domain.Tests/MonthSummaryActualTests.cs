using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class MonthSummaryActualTests
{
    private static readonly YearMonth May = new(2026, 5);

    private readonly RecurringLine _salary = Income("Salary", 2000m, day: 28);
    private readonly RecurringLine _rent = Expense("Rent", 650m, day: 5);
    private readonly RecurringLine _water = Expense("Water", 45m, day: 15);
    private readonly Budget _groceries = NewBudget("Groceries", 200m);

    [Fact]
    public void Actual_WithoutAnyActualOrOverride_EqualsThePlan()
    {
        Assert.Equal(Planned(), Actual([], []));
    }

    [Fact]
    public void Actual_BudgetActual_ReplacesThePlannedAmount()
    {
        var actual = Actual([new BudgetActual(_groceries.Id, April, 235m)], []);

        Assert.Equal(235m, actual.BudgetsTotal);
        Assert.Equal(2000m - 695m - 235m, actual.CashSavingsCapacity);
    }

    [Fact]
    public void Actual_BudgetActualOfZero_MeansNothingWasSpent()
    {
        var actual = Actual([new BudgetActual(_groceries.Id, April, 0m)], []);

        Assert.Equal(0m, actual.BudgetsTotal);
    }

    [Fact]
    public void Actual_OverrideWithAmountOnAnExpense_ReplacesItsAmount()
    {
        var actual = Actual([], [RecurringOverride.WithAmount(_water.Id, April, 52m)]);

        Assert.Equal(650m + 52m, actual.CashRecurringExpenses);
    }

    [Fact]
    public void Actual_OverrideWithAmountOnAnIncome_ReplacesItsAmount()
    {
        var actual = Actual([], [RecurringOverride.WithAmount(_salary.Id, April, 2100m)]);

        Assert.Equal(2100m, actual.CashIncomes);
    }

    [Fact]
    public void Actual_SkippedIncome_CountsForNothing()
    {
        var actual = Actual([], [RecurringOverride.Skipped(_salary.Id, April)]);

        Assert.Equal(0m, actual.CashIncomes);
    }

    [Fact]
    public void Actual_SkippedExpense_CountsForNothing()
    {
        var actual = Actual([], [RecurringOverride.Skipped(_rent.Id, April)]);

        Assert.Equal(45m, actual.CashRecurringExpenses);
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
    public void Actual_OverridesOfTheSameLineInTwoMonths_AreNotDuplicates()
    {
        var actual = Actual([],
            [RecurringOverride.WithAmount(_water.Id, April, 52m), RecurringOverride.WithAmount(_water.Id, May, 60m)]);

        Assert.Equal(650m + 52m, actual.CashRecurringExpenses);
    }

    [Fact]
    public void Actual_OverrideOfALineNotDueThisMonth_IsIgnored()
    {
        var insurance = YearlyExpense("Car insurance", 360m, day: 10, anchorMonth: 3);

        var actual = MonthSummary.Actual(April, [insurance], [], [], [RecurringOverride.WithAmount(insurance.Id, April, 400m)]);

        Assert.Equal(0m, actual.CashRecurringExpenses);
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
    public void RecurringOverride_WithAmountNotPositive_Throws(int amount)
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
}
