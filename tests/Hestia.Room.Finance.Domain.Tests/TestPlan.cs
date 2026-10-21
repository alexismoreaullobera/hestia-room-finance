namespace Hestia.Room.Finance.Domain.Tests;

/// <summary>Shared builders of the domain tests: a line or a budget in one short call.</summary>
internal static class TestPlan
{
    /// <summary>A start date far in the past: the line is active in every month of the tests.</summary>
    public static readonly DateOnly LongAgo = new(2020, 1, 1);

    /// <summary>April 2026: 30 days, starts on a Wednesday.</summary>
    public static readonly YearMonth April = new(2026, 4);

    public static RecurringLine Income(string name, decimal amount, int day) =>
        Monthly(FlowKind.Income, name, amount, day);

    public static RecurringLine Expense(string name, decimal amount, int day, DateOnly? start = null, DateOnly? end = null) =>
        Monthly(FlowKind.Expense, name, amount, day, start, end);

    public static RecurringLine Monthly(FlowKind kind, string name, decimal amount, int day, DateOnly? start = null, DateOnly? end = null) =>
        new(Guid.NewGuid(), kind, name, amount, Frequency.Monthly, day, start ?? LongAgo, endDate: end);

    public static RecurringLine YearlyExpense(string name, decimal amount, int day, int anchorMonth, DateOnly? start = null) =>
        new(Guid.NewGuid(), FlowKind.Expense, name, amount, Frequency.Yearly, day, start ?? LongAgo, anchorMonth);

    public static Budget NewBudget(string name, decimal amount) => new(Guid.NewGuid(), name, amount);
}
