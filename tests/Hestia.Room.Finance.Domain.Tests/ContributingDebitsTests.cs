using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class ContributingDebitsTests
{
    [Fact]
    public void ContributingDebits_AreTheExpensesBetweenTheStartAndTheLowPoint()
    {
        var rent = Expense("Rent", 650m, day: 5);
        var mobile = Expense("Mobile plan", 15.99m, day: 8);
        var streaming = Expense("Streaming", 13.49m, day: 29); // after the low point
        var salary = Income("Salary", 2000m, day: 28);

        var forecast = CashFlowForecast.Compute(April, 0m, [salary, streaming, mobile, rent], []);

        Assert.Equal([rent, mobile], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_IncludeExpensesBeforeAnIncomeThatDoesNotRecoverThePeak()
    {
        // 5: rent -650 → -650 · 10: benefit +300 → -350 · 20: insurance -400 → -750 (low point).
        // The benefit does not bring the balance back to its peak (0 €): the rent still digs the low point.
        var rent = Expense("Rent", 650m, day: 5);
        var insurance = Expense("Car insurance", 400m, day: 20);

        var forecast = CashFlowForecast.Compute(April, 0m,
            [rent, Income("Benefit", 300m, day: 10), insurance, Income("Salary", 2000m, day: 28)], []);

        Assert.Equal(new DateOnly(2026, 4, 20), forecast.LowPoint.Date);
        Assert.Equal([rent, insurance], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_ExcludeExpensesBeforeAHigherPeak()
    {
        // 5: rent -650 → -650 · 10: salary +2000 → 1350 (new peak) · 20: works -2100 → -750 (low point).
        var works = Expense("Works", 2100m, day: 20);

        var forecast = CashFlowForecast.Compute(April, 0m, [Expense("Rent", 650m, day: 5), Income("Salary", 2000m, day: 10), works], []);

        Assert.Equal(new DateOnly(2026, 4, 20), forecast.LowPoint.Date);
        Assert.Equal([works], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_ExcludeExpensesBeforeAnIncomeThatRecoversThePeakExactly()
    {
        // 5: rent -650 → -650 · 10: benefit +650 → 0 (back to the peak) · 20: works -900 → -900 (low point).
        // On equal balances, the latest day of the peak is kept: the rent is behind it.
        var works = Expense("Works", 900m, day: 20);

        var forecast = CashFlowForecast.Compute(April, 0m, [Expense("Rent", 650m, day: 5), Income("Benefit", 650m, day: 10), works], []);

        Assert.Equal([works], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_IncludeTheExpenseOnTheLowPointDay()
    {
        var rent = Expense("Rent", 650m, day: 5);

        var forecast = CashFlowForecast.Compute(April, 0m, [rent, Income("Salary", 2000m, day: 28)], []);

        Assert.Equal(new DateOnly(2026, 4, 5), forecast.LowPoint.Date);
        Assert.Equal([rent], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_WithALowPointOnTheFirstDay_AreTheExpensesOfThatDay()
    {
        var rent = Expense("Rent", 650m, day: 1);

        var forecast = CashFlowForecast.Compute(April, 0m, [rent, Income("Salary", 2000m, day: 28)], []);

        Assert.Equal([rent], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_DoNotListIncomesNorBudgets()
    {
        var rent = Expense("Rent", 650m, day: 5);

        var forecast = CashFlowForecast.Compute(April, 0m, [Income("Benefit", 100m, day: 3), rent], [NewBudget("Groceries", 300m)]);

        Assert.Equal([rent], forecast.ContributingDebits.Select(o => o.Line));
    }

    [Fact]
    public void ContributingDebits_WhenTheMonthNeedsNoBuffer_AreEmpty()
    {
        var forecast = CashFlowForecast.Compute(April, 0m, [Income("Salary", 2000m, day: 1), Expense("Rent", 650m, day: 5)], []);

        Assert.Equal(0m, forecast.CashBuffer);
        Assert.Empty(forecast.ContributingDebits);
    }

    [Fact]
    public void ContributingDebits_DoNotDependOnTheStartingBalance()
    {
        RecurringLine[] plan = [Expense("Rent", 650m, day: 5), Income("Benefit", 300m, day: 10), Expense("Car insurance", 400m, day: 20)];

        var fromZero = CashFlowForecast.Compute(April, 0m, plan, []);
        var withBuffer = CashFlowForecast.Compute(April, 1000m, plan, []);

        Assert.Equal(fromZero.ContributingDebits, withBuffer.ContributingDebits);
    }
}
