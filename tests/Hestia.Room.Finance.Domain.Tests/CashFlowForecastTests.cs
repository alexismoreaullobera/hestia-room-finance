namespace Hestia.Room.Finance.Domain.Tests;

public class CashFlowForecastTests
{
    private static readonly YearMonth April = new(2026, 4); // 30 days
    private static readonly DateOnly LongAgo = new(2020, 1, 1);

    [Fact]
    public void Compute_HasOneBalancePerDayOfTheMonth()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [], []);

        Assert.Equal(30, forecast.Days.Count);
        Assert.Equal(new DateOnly(2026, 4, 1), forecast.Days[0].Date);
        Assert.Equal(new DateOnly(2026, 4, 30), forecast.Days[^1].Date);
    }

    [Fact]
    public void Compute_WithoutAnyFlow_StaysAtTheStartingBalance()
    {
        var forecast = CashFlowForecast.Compute(April, 30m, [], []);

        Assert.Equal(30m, forecast.StartingBalance);
        Assert.All(forecast.Days, day => Assert.Equal(30m, day.Balance));
    }

    [Fact]
    public void Compute_RecurringLines_MoveTheBalanceOnTheirDueDate()
    {
        var forecast = CashFlowForecast.Compute(April, 100m,
            [Monthly(FlowKind.Expense, 650m, day: 5), Monthly(FlowKind.Income, 2000m, day: 28)], []);

        Assert.Equal(100m, BalanceOn(forecast, 4));
        Assert.Equal(-550m, BalanceOn(forecast, 5));
        Assert.Equal(-550m, BalanceOn(forecast, 27));
        Assert.Equal(1450m, BalanceOn(forecast, 28));
        Assert.Equal(1450m, BalanceOn(forecast, 30));
    }

    [Fact]
    public void Compute_IncomeAndExpenseOnTheSameDay_AreNetted()
    {
        var forecast = CashFlowForecast.Compute(April, 0m,
            [Monthly(FlowKind.Income, 1000m, day: 1), Monthly(FlowKind.Expense, 650m, day: 1)], []);

        Assert.Equal(350m, BalanceOn(forecast, 1));
    }

    [Fact]
    public void Compute_LineDueOnThe31st_MovesTheBalanceOnTheLastDayOfApril()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [Monthly(FlowKind.Expense, 15m, day: 31)], []);

        Assert.Equal(0m, BalanceOn(forecast, 29));
        Assert.Equal(-15m, BalanceOn(forecast, 30));
    }

    [Fact]
    public void Compute_BudgetDivisibleByTheDays_IsSpentEvenly()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [], [new Budget(Guid.NewGuid(), "Groceries", 300m)]);

        Assert.Equal(-10m, BalanceOn(forecast, 1));
        Assert.Equal(-150m, BalanceOn(forecast, 15));
        Assert.Equal(-300m, BalanceOn(forecast, 30));
    }

    [Fact]
    public void Compute_BudgetNotDivisibleByTheDays_PutsTheRemainderOnTheLastDay()
    {
        // 200 € over 30 days: 6.67 a day for 29 days (193.43), then 6.57 on the last day.
        var forecast = CashFlowForecast.Compute(April, 0m, [], [new Budget(Guid.NewGuid(), "Groceries", 200m)]);

        Assert.Equal(-6.67m, BalanceOn(forecast, 1));
        Assert.Equal(-193.43m, BalanceOn(forecast, 29));
        Assert.Equal(-200m, BalanceOn(forecast, 30));
    }

    [Fact]
    public void Compute_EndOfMonthBalance_IsTheStartingBalancePlusThePlannedSavingsCapacity()
    {
        RecurringLine[] lines =
        [
            Monthly(FlowKind.Income, 2000m, day: 28),
            Monthly(FlowKind.Expense, 650m, day: 5),
            Monthly(FlowKind.Expense, 15.99m, day: 31),
        ];
        Budget[] budgets = [new(Guid.NewGuid(), "Groceries", 200m), new(Guid.NewGuid(), "Outings", 80m)];

        var forecast = CashFlowForecast.Compute(April, 30m, lines, budgets);
        var summary = MonthSummary.Planned(April, lines, budgets);

        Assert.Equal(30m + summary.SavingsCapacity, forecast.Days[^1].Balance);
    }

    [Fact]
    public void Compute_Occurrences_AreTheLinesDueThisMonthByDate()
    {
        var rent = Monthly(FlowKind.Expense, 650m, day: 5);
        var salary = Monthly(FlowKind.Income, 2000m, day: 28);
        var insurance = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Car insurance", 360m, Frequency.Yearly, 10, LongAgo, anchorMonth: 3);

        var forecast = CashFlowForecast.Compute(April, 0m, [salary, insurance, rent], []);

        Assert.Equal([rent, salary], forecast.Occurrences.Select(o => o.Line));
    }

    private static decimal BalanceOn(CashFlowForecast forecast, int day) => forecast.Days[day - 1].Balance;

    private static RecurringLine Monthly(FlowKind kind, decimal amount, int day) =>
        new(Guid.NewGuid(), kind, "Line", amount, Frequency.Monthly, day, LongAgo);
}
