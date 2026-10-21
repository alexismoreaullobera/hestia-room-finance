using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class MonthSummaryPlannedTests
{
    private static readonly YearMonth March = new(2026, 3);

    [Fact]
    public void Planned_WithAnEmptyPlan_IsZero()
    {
        var summary = MonthSummary.Planned(April, [], []);

        Assert.Equal(April, summary.Month);
        Assert.Equal(0m, summary.CashSavingsCapacity);
        Assert.Equal(0m, summary.SmoothedSavingsCapacity);
    }

    [Fact]
    public void Planned_SavingsCapacity_IsIncomesMinusRecurringExpensesMinusBudgets()
    {
        RecurringLine[] lines =
        [
            Income("Salary", 2000m, day: 28),
            Expense("Rent", 650m, day: 5),
            Expense("Mobile plan", 15.99m, day: 8),
        ];
        Budget[] budgets = [NewBudget("Groceries", 200m), NewBudget("Outings", 80m)];

        var summary = MonthSummary.Planned(April, lines, budgets);

        Assert.Equal(2000m, summary.CashIncomes);
        Assert.Equal(665.99m, summary.CashRecurringExpenses);
        Assert.Equal(280m, summary.BudgetsTotal);
        Assert.Equal(1054.01m, summary.CashSavingsCapacity);
    }

    [Fact]
    public void Planned_SavingsCapacity_CanBeNegative()
    {
        var summary = MonthSummary.Planned(April,
            [Income("Benefit", 900m, day: 2), Expense("Rent", 650m, day: 5)],
            [NewBudget("Groceries", 300m)]);

        Assert.Equal(-50m, summary.CashSavingsCapacity);
    }

    [Fact]
    public void Planned_YearlyExpense_WeighsFullyInItsMonthButIsSmoothedEveryMonth()
    {
        var insurance = YearlyExpense("Car insurance", 360m, day: 10, anchorMonth: 3);

        var march = MonthSummary.Planned(March, [insurance], []);
        var april = MonthSummary.Planned(April, [insurance], []);

        // Cash reading: the whole debit in March, nothing in April.
        Assert.Equal(360m, march.CashRecurringExpenses);
        Assert.Equal(0m, april.CashRecurringExpenses);
        // Smoothed reading: 30 € both months.
        Assert.Equal(30m, march.SmoothedRecurringExpenses);
        Assert.Equal(30m, april.SmoothedRecurringExpenses);
        Assert.Equal(-30m, april.SmoothedSavingsCapacity);
    }

    [Fact]
    public void Planned_LineStartingAfterTheMonth_IsIgnoredInTheSmoothedReading()
    {
        var insurance = YearlyExpense("Car insurance", 360m, day: 10, anchorMonth: 3, start: new DateOnly(2026, 6, 1));

        var summary = MonthSummary.Planned(April, [insurance], []);

        Assert.Equal(0m, summary.SmoothedRecurringExpenses);
    }

    [Fact]
    public void Planned_LineEndedBeforeTheMonth_IsIgnoredInBothReadings()
    {
        var gym = Expense("Gym", 30m, day: 5, end: new DateOnly(2026, 2, 28));

        var summary = MonthSummary.Planned(April, [gym], []);

        Assert.Equal(0m, summary.CashRecurringExpenses);
        Assert.Equal(0m, summary.SmoothedRecurringExpenses);
    }

    [Fact]
    public void Planned_LineEndingDuringTheMonthBeforeItsDueDate_IsOnlyInTheSmoothedReading()
    {
        // Ends on April 3rd, due on the 5th: no April debit, but the line still runs part of April.
        var gym = Expense("Gym", 30m, day: 5, end: new DateOnly(2026, 4, 3));

        var summary = MonthSummary.Planned(April, [gym], []);

        Assert.Equal(0m, summary.CashRecurringExpenses);
        Assert.Equal(30m, summary.SmoothedRecurringExpenses);
    }

    [Fact]
    public void Planned_LineEndingOnTheFirstDayOfTheMonth_StillRunsThatMonth()
    {
        var gym = Expense("Gym", 30m, day: 5, end: April.FirstDay);

        Assert.Equal(30m, MonthSummary.Planned(April, [gym], []).SmoothedRecurringExpenses);
    }
}
