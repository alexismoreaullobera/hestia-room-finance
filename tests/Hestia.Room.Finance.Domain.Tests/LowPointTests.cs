using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class LowPointTests
{
    // Rent on the 5th, salary on the 28th: the account goes down to -650 € from the 5th to the 27th.
    private static readonly RecurringLine[] Plan = [Expense("Rent", 650m, day: 5), Income("Salary", 2000m, day: 28)];

    [Fact]
    public void LowPoint_IsTheLowestEndOfDayBalance_OnTheEarliestDayIfSeveral()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, Plan, []);

        Assert.Equal(new DailyBalance(new DateOnly(2026, 4, 5), -650m), forecast.LowPoint);
    }

    [Fact]
    public void LowPoint_WithBudgets_IsTheDayBeforeTheIncome()
    {
        // Groceries spread every day: the balance keeps going down until the salary on the 28th.
        var forecast = CashFlowForecast.Compute(April, 0m, Plan, [NewBudget("Groceries", 300m)]);

        Assert.Equal(new DateOnly(2026, 4, 27), forecast.LowPoint.Date);
        Assert.Equal(-650m - (27 * 10m), forecast.LowPoint.Balance);
    }

    [Fact]
    public void LowPoint_CanBeTheFirstDayOfTheMonth()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [Expense("Rent", 650m, day: 1), Income("Salary", 2000m, day: 28)], []);

        Assert.Equal(April.FirstDay, forecast.LowPoint.Date);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(1000)]
    [InlineData(-100)]
    public void CashBuffer_IsTheDepthOfTheMonth_WhateverTheStartingBalance(int startingBalance)
    {
        var forecast = CashFlowForecast.Compute(April, startingBalance, Plan, []);

        Assert.Equal(650m, forecast.CashBuffer);
    }

    [Fact]
    public void CashBuffer_WhenTheForecastNeverGoesDown_IsZero()
    {
        // Salary on the 1st, rent on the 5th: the account never goes below its starting balance.
        var forecast = CashFlowForecast.Compute(April, 0m, [Income("Salary", 2000m, day: 1), Expense("Rent", 650m, day: 5)], []);

        Assert.Equal(0m, forecast.CashBuffer);
        Assert.False(forecast.HasAlert);
    }

    public static TheoryData<decimal, bool> StartingBalancesAndAlert => new()
    {
        { 0m, true },
        { 649.99m, true },
        { 650m, false }, // exactly the buffer: the balance reaches 0 €, not below
        { 1000m, false },
    };

    [Theory]
    [MemberData(nameof(StartingBalancesAndAlert))]
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

    [Fact]
    public void HasAlert_WithANegativeStartingBalanceAndNoFlow_IsTrueWithoutAnyDebitToMove()
    {
        // The account is already overdrawn: the month itself needs no buffer, there is nothing to move.
        var forecast = CashFlowForecast.Compute(April, -100m, [], []);

        Assert.True(forecast.HasAlert);
        Assert.Equal(0m, forecast.CashBuffer);
        Assert.Empty(forecast.ContributingDebits);
    }
}
