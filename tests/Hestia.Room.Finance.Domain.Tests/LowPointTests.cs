namespace Hestia.Room.Finance.Domain.Tests;

public class LowPointTests
{
    private static readonly YearMonth April = new(2026, 4);
    private static readonly DateOnly LongAgo = new(2020, 1, 1);

    // Rent on the 5th, salary on the 28th: the account goes down until the 27th.
    private static readonly RecurringLine[] Plan =
    [
        Monthly(FlowKind.Expense, 650m, day: 5),
        Monthly(FlowKind.Income, 2000m, day: 28),
    ];

    [Fact]
    public void LowPoint_IsTheLowestEndOfDayBalanceWithItsDate()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, Plan, []);

        Assert.Equal(new DailyBalance(new DateOnly(2026, 4, 5), -650m), forecast.LowPoint);
    }

    [Fact]
    public void LowPoint_OnSeveralDaysAtTheSameBalance_IsTheEarliestDay()
    {
        // The balance stays at -650 € from the 5th to the 27th.
        var forecast = CashFlowForecast.Compute(April, 0m, Plan, []);

        Assert.Equal(new DateOnly(2026, 4, 5), forecast.LowPoint.Date);
    }

    [Fact]
    public void LowPoint_WithBudgets_IsTheDayBeforeTheIncome()
    {
        // Groceries spread every day: the balance keeps going down until the salary on the 28th.
        var forecast = CashFlowForecast.Compute(April, 0m, Plan, [new Budget(Guid.NewGuid(), "Groceries", 300m)]);

        Assert.Equal(new DateOnly(2026, 4, 27), forecast.LowPoint.Date);
        Assert.Equal(-650m - (27 * 10m), forecast.LowPoint.Balance);
    }

    [Fact]
    public void CashBuffer_IsHowFarTheForecastGoesBelowZeroFromZero()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, Plan, []);

        Assert.Equal(650m, forecast.CashBuffer);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(1000)]
    public void CashBuffer_DoesNotDependOnTheStartingBalance(decimal startingBalance)
    {
        var forecast = CashFlowForecast.Compute(April, startingBalance, Plan, []);

        Assert.Equal(650m, forecast.CashBuffer);
    }

    [Fact]
    public void CashBuffer_WhenTheForecastNeverGoesDown_IsZero()
    {
        // Salary on the 1st, rent on the 5th: the account never goes below its starting balance.
        var forecast = CashFlowForecast.Compute(April, 0m,
            [Monthly(FlowKind.Income, 2000m, day: 1), Monthly(FlowKind.Expense, 650m, day: 5)], []);

        Assert.Equal(0m, forecast.CashBuffer);
        Assert.False(forecast.HasAlert);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(649.99, true)]
    [InlineData(650, false)] // exactly the buffer: the balance reaches 0 €, not below
    [InlineData(1000, false)]
    public void HasAlert_WhenTheStartingBalanceDoesNotCoverTheBuffer(decimal startingBalance, bool expectedAlert)
    {
        var forecast = CashFlowForecast.Compute(April, startingBalance, Plan, []);

        Assert.Equal(expectedAlert, forecast.HasAlert);
    }

    [Fact]
    public void HasAlert_WithAnEmptyPlan_IsFalse()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [], []);

        Assert.False(forecast.HasAlert);
        Assert.Equal(0m, forecast.CashBuffer);
    }

    private static RecurringLine Monthly(FlowKind kind, decimal amount, int day) =>
        new(Guid.NewGuid(), kind, "Line", amount, Frequency.Monthly, day, LongAgo);
}
