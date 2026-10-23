using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Demo;

namespace Hestia.Room.Finance.Infrastructure.Tests.Demo;

public class DemoPlanTests
{
    private static readonly YearMonth CurrentMonth = new(2026, 10);
    private static readonly DemoPlan Demo = DemoPlan.For(CurrentMonth);

    [Fact]
    public void Demo_HasIncomesExpensesSubscriptionsAndBudgets()
    {
        Assert.Contains(Demo.Lines, line => line.Kind == FlowKind.Income);
        Assert.Contains(Demo.Lines, line => line.Kind == FlowKind.Expense && !line.IsSubscription);
        Assert.Contains(Demo.Lines, line => line.IsSubscription && line.Subscription!.CommitmentEndDate is not null);
        Assert.Contains(Demo.Lines, line => line.Frequency == Frequency.Quarterly);
        Assert.Contains(Demo.Lines, line => line.Frequency == Frequency.Yearly);
        Assert.NotEmpty(Demo.Budgets);
    }

    [Fact]
    public void Demo_HasActualsForEachOfThePastMonths()
    {
        var monthsWithActuals = Demo.BudgetActuals.Select(a => a.Month).Distinct().OrderBy(m => m.FirstDay).ToList();

        Assert.Equal([new YearMonth(2026, 7), new YearMonth(2026, 8), new YearMonth(2026, 9)], monthsWithActuals);
        Assert.All(monthsWithActuals, month =>
            Assert.Equal(Demo.Budgets.Count, Demo.BudgetActuals.Count(a => a.Month == month)));
    }

    [Fact]
    public void Demo_HasAnOverrideWithAmountAndASkippedLine()
    {
        Assert.Contains(Demo.Overrides, o => !o.IsSkipped);
        Assert.Contains(Demo.Overrides, o => o.IsSkipped);
        Assert.All(Demo.Overrides, o => Assert.Contains(Demo.Lines, line => line.Id == o.LineId));
    }

    /// <summary>Two years of current months, from March 2027: the demo runs on the real current month, whatever it is.</summary>
    public static TheoryData<int, int> CurrentMonths()
    {
        var data = new TheoryData<int, int>();
        foreach (var month in Enumerable.Range(0, 24).Select(i => new YearMonth(2027, 3).AddMonths(i)))
        {
            data.Add(month.Year, month.Month);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(CurrentMonths))] // includes February 2028, a leap year
    public void Demo_AnyCurrentMonth_ShowsALowPointAndAnAlertWithItsDebits(int year, int monthNumber)
    {
        var currentMonth = new YearMonth(year, monthNumber);
        var demo = DemoPlan.For(currentMonth);
        var month = new FinanceMonth(demo.MonthPlans.Single(p => p.Month == currentMonth), demo.Lines, demo.Budgets, [], []);

        var forecast = month.Forecast();

        Assert.True(forecast.CashBuffer > 0m);
        Assert.True(forecast.HasAlert, "The demo is meant to show the alert.");
        Assert.NotEmpty(forecast.ContributingDebits);
        Assert.True(month.PlannedSummary().CashSavingsCapacity > 0m, "Over the month, the demo plan saves money.");
    }

    [Fact]
    public void Demo_IsBuiltRelativeToTheCurrentMonth()
    {
        var later = DemoPlan.For(new YearMonth(2027, 3));

        Assert.Equal(new YearMonth(2027, 3), later.MonthPlans.Single().Month);
        Assert.Equal(new YearMonth(2026, 12), later.BudgetActuals.MinBy(a => a.Month.FirstDay)!.Month);
    }

    [Fact]
    public void Demo_IdentifiersAreUnique()
    {
        var ids = Demo.Lines.Select(l => l.Id).Concat(Demo.Budgets.Select(b => b.Id)).ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
