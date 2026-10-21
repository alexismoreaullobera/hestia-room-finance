using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class CashFlowForecastTests
{
    [Theory]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)] // leap year
    [InlineData(2026, 1, 31)]
    public void Compute_HasOneBalancePerDayOfTheMonth(int year, int month, int expectedDays)
    {
        var forecast = CashFlowForecast.Compute(new YearMonth(year, month), 0m, [], []);

        Assert.Equal(expectedDays, forecast.Days.Count);
        Assert.Equal(new DateOnly(year, month, 1), forecast.Days[0].Date);
        Assert.Equal(new DateOnly(year, month, expectedDays), forecast.Days[^1].Date);
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
        var forecast = CashFlowForecast.Compute(April, 100m, [Expense("Rent", 650m, day: 5), Income("Salary", 2000m, day: 28)], []);

        Assert.Equal(100m, BalanceOn(forecast, 4));
        Assert.Equal(-550m, BalanceOn(forecast, 5));
        Assert.Equal(-550m, BalanceOn(forecast, 27));
        Assert.Equal(1450m, BalanceOn(forecast, 28));
        Assert.Equal(1450m, BalanceOn(forecast, 30));
    }

    [Fact]
    public void Compute_IncomeAndExpenseOnTheSameDay_AreNetted()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [Income("Benefit", 1000m, day: 1), Expense("Rent", 650m, day: 1)], []);

        Assert.Equal(350m, BalanceOn(forecast, 1));
    }

    [Theory]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)]
    [InlineData(2026, 1, 31)]
    public void Compute_LineDueOnThe31st_MovesTheBalanceOnTheLastDayOfTheMonth(int year, int month, int lastDay)
    {
        var forecast = CashFlowForecast.Compute(new YearMonth(year, month), 0m, [Expense("Mobile plan", 15m, day: 31)], []);

        Assert.Equal(0m, BalanceOn(forecast, lastDay - 1));
        Assert.Equal(-15m, BalanceOn(forecast, lastDay));
    }

    [Fact]
    public void Compute_BudgetDivisibleByTheDays_IsSpentEvenly()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [], [NewBudget("Groceries", 300m)]);

        Assert.Equal(-10m, BalanceOn(forecast, 1));
        Assert.Equal(-150m, BalanceOn(forecast, 15));
        Assert.Equal(-300m, BalanceOn(forecast, 30));
    }

    [Fact]
    public void Compute_BudgetNotDivisibleByTheDays_GivesTheRemainingCentsToTheFirstDays()
    {
        // 200 € over 30 days: 6.66 a day leaves 20 cents, given one by one to the first 20 days.
        var forecast = CashFlowForecast.Compute(April, 0m, [], [NewBudget("Groceries", 200m)]);
        var shares = DailyShares(forecast);

        Assert.All(shares.Take(20), share => Assert.Equal(6.67m, share));
        Assert.All(shares.Skip(20), share => Assert.Equal(6.66m, share));
        Assert.Equal(-200m, BalanceOn(forecast, 30));
    }

    public static TheoryData<decimal, int, int> BudgetAmountsAndMonths => new()
    {
        { 2m, 2026, 4 },      // rounding to the nearest cent would give a negative last share here
        { 0.45m, 2026, 4 },
        { 0.5m, 2026, 1 },
        { 4.35m, 2026, 4 },
        { 200m, 2026, 4 },
        { 280.33m, 2026, 2 },
        { 1000m, 2028, 2 },
        { 12.345m, 2026, 1 }, // more than two decimals
    };

    [Theory]
    [MemberData(nameof(BudgetAmountsAndMonths))]
    public void Compute_BudgetShares_AreNeverNegativeAndAddUpExactlyToTheBudget(decimal amount, int year, int month)
    {
        var forecast = CashFlowForecast.Compute(new YearMonth(year, month), 0m, [], [NewBudget("Budget", amount)]);
        var shares = DailyShares(forecast);

        Assert.All(shares, share => Assert.True(share >= 0m, $"Negative share {share}"));
        Assert.Equal(amount, shares.Sum());
    }

    [Fact]
    public void Compute_EndOfMonthBalance_IsTheStartingBalancePlusThePlannedSavingsCapacity()
    {
        RecurringLine[] lines =
        [
            Income("Salary", 2000m, day: 28),
            Expense("Rent", 650m, day: 5),
            Expense("Mobile plan", 15.99m, day: 31),
            YearlyExpense("Car insurance", 360m, day: 10, anchorMonth: 4),
        ];
        Budget[] budgets = [NewBudget("Groceries", 200m), NewBudget("Outings", 80m)];

        var forecast = CashFlowForecast.Compute(April, 30m, lines, budgets);
        var summary = MonthSummary.Planned(April, lines, budgets);

        Assert.Equal(30m + summary.CashSavingsCapacity, forecast.Days[^1].Balance);
    }

    [Fact]
    public void Compute_Occurrences_AreTheLinesDueThisMonthByDate()
    {
        var rent = Expense("Rent", 650m, day: 5);
        var salary = Income("Salary", 2000m, day: 28);
        var insurance = YearlyExpense("Car insurance", 360m, day: 10, anchorMonth: 3);

        var forecast = CashFlowForecast.Compute(April, 0m, [salary, insurance, rent], []);

        Assert.Equal([rent, salary], forecast.Occurrences.Select(o => o.Line));
    }

    private static decimal BalanceOn(CashFlowForecast forecast, int day) => forecast.Days[day - 1].Balance;

    /// <summary>What each day costs, read from the series (forecast without recurring lines).</summary>
    private static decimal[] DailyShares(CashFlowForecast forecast) =>
        forecast.Days
            .Select((day, index) => (index == 0 ? forecast.StartingBalance : forecast.Days[index - 1].Balance) - day.Balance)
            .ToArray();
}
