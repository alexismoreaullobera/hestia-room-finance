using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class FinanceMonthTests
{
    private static readonly YearMonth May = new(2026, 5);

    private readonly RecurringLine _salary = Income("Salary", 2000m, day: 28);
    private readonly RecurringLine _rent = Expense("Rent", 650m, day: 5);
    private readonly Budget _groceries = NewBudget("Groceries", 200m);

    [Fact]
    public void Calculations_AreTheOnesOfTheDomain()
    {
        var actual = new BudgetActual(_groceries.Id, April, 235m);
        var month = Month(startingBalance: 30m, actuals: [actual]);

        Assert.Equal(MonthSummary.Planned(April, [_salary, _rent], [_groceries]), month.PlannedSummary());
        Assert.Equal(MonthSummary.Actual(April, [_salary, _rent], [_groceries], [actual], []), month.ActualSummary());
        Assert.Equal(CashFlowForecast.Compute(April, 30m, [_salary, _rent], [_groceries]).Days, month.Forecast().Days);
    }

    [Fact]
    public void Forecast_StartsFromTheStartingBalanceOfThePlan()
    {
        Assert.Equal(30m, Month(startingBalance: 30m).Forecast().StartingBalance);
    }

    [Fact]
    public void Month_IsTheMonthOfThePlan()
    {
        Assert.Equal(April, Month().Month);
    }

    [Fact]
    public void Constructor_WithAnActualOfAnotherMonth_Throws()
    {
        Assert.Throws<ArgumentException>(() => Month(actuals: [new BudgetActual(_groceries.Id, May, 100m)]));
    }

    [Fact]
    public void Constructor_WithAnOverrideOfAnotherMonth_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new FinanceMonth(new MonthPlan(April), [_rent], [], [], [RecurringOverride.Skipped(_rent.Id, May)]));
    }

    private FinanceMonth Month(decimal startingBalance = 0m, BudgetActual[]? actuals = null) =>
        new(new MonthPlan(April, startingBalance), [_salary, _rent], [_groceries], actuals ?? [], []);
}
