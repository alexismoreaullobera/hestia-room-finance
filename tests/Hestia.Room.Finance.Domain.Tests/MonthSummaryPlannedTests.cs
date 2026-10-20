namespace Hestia.Room.Finance.Domain.Tests;

public class MonthSummaryPlannedTests
{
    private static readonly YearMonth March = new(2026, 3);
    private static readonly YearMonth April = new(2026, 4);
    private static readonly DateOnly LongAgo = new(2020, 1, 1);

    [Fact]
    public void Planned_WithAnEmptyPlan_IsZero()
    {
        var summary = MonthSummary.Planned(April, [], []);

        Assert.Equal(April, summary.Month);
        Assert.Equal(0m, summary.SavingsCapacity);
        Assert.Equal(0m, summary.SmoothedSavingsCapacity);
    }

    [Fact]
    public void Planned_SavingsCapacity_IsIncomesMinusRecurringExpensesMinusBudgets()
    {
        RecurringLine[] lines =
        [
            Monthly(FlowKind.Income, "Salary", 2000m, day: 28),
            Monthly(FlowKind.Expense, "Rent", 650m, day: 5),
            Monthly(FlowKind.Expense, "Mobile plan", 15.99m, day: 8),
        ];
        Budget[] budgets = [new(Guid.NewGuid(), "Groceries", 200m), new(Guid.NewGuid(), "Outings", 80m)];

        var summary = MonthSummary.Planned(April, lines, budgets);

        Assert.Equal(2000m, summary.Incomes);
        Assert.Equal(665.99m, summary.RecurringExpenses);
        Assert.Equal(280m, summary.Budgets);
        Assert.Equal(1054.01m, summary.SavingsCapacity);
    }

    [Fact]
    public void Planned_SavingsCapacity_CanBeNegative()
    {
        var summary = MonthSummary.Planned(April,
            [Monthly(FlowKind.Income, "Benefit", 900m, day: 2), Monthly(FlowKind.Expense, "Rent", 650m, day: 5)],
            [new Budget(Guid.NewGuid(), "Groceries", 300m)]);

        Assert.Equal(-50m, summary.SavingsCapacity);
    }

    [Fact]
    public void Planned_YearlyExpense_WeighsFullyInItsMonthButIsSmoothedEveryMonth()
    {
        var insurance = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Car insurance", 360m, Frequency.Yearly, 10, LongAgo, anchorMonth: 3);

        var march = MonthSummary.Planned(March, [insurance], []);
        var april = MonthSummary.Planned(April, [insurance], []);

        // Cash reading: the whole debit in March, nothing in April.
        Assert.Equal(360m, march.RecurringExpenses);
        Assert.Equal(0m, april.RecurringExpenses);
        // Smoothed reading: 30 € both months.
        Assert.Equal(30m, march.SmoothedRecurringExpenses);
        Assert.Equal(30m, april.SmoothedRecurringExpenses);
        Assert.Equal(-30m, april.SmoothedSavingsCapacity);
    }

    [Fact]
    public void Planned_LineEndedBeforeTheMonth_IsIgnoredInBothReadings()
    {
        var gym = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Gym", 30m, Frequency.Monthly, 5, LongAgo, endDate: new DateOnly(2026, 2, 28));

        var summary = MonthSummary.Planned(April, [gym], []);

        Assert.Equal(0m, summary.RecurringExpenses);
        Assert.Equal(0m, summary.SmoothedRecurringExpenses);
    }

    [Fact]
    public void Planned_LineEndingDuringTheMonthBeforeItsDueDate_IsOnlyInTheSmoothedReading()
    {
        // Ends on April 3rd, due on the 5th: no April debit, but the line still runs part of April.
        var gym = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Gym", 30m, Frequency.Monthly, 5, LongAgo, endDate: new DateOnly(2026, 4, 3));

        var summary = MonthSummary.Planned(April, [gym], []);

        Assert.Equal(0m, summary.RecurringExpenses);
        Assert.Equal(30m, summary.SmoothedRecurringExpenses);
    }

    private static RecurringLine Monthly(FlowKind kind, string name, decimal amount, int day) =>
        new(Guid.NewGuid(), kind, name, amount, Frequency.Monthly, day, LongAgo);
}
