using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

/// <summary>A small plan for April 2026, built in memory: the projections are tested without any database.</summary>
internal static class TestMonth
{
    public static readonly YearMonth April = new(2026, 4);

    private static readonly DateOnly LongAgo = new(2025, 1, 1);

    public static RecurringLine Monthly(FlowKind kind, string name, decimal amount, int day) =>
        new(Guid.CreateVersion7(), kind, name, amount, Frequency.Monthly, day, LongAgo);

    /// <summary>A quarterly line due in April: 100 € this month, 33.333… € as a monthly equivalent.</summary>
    public static RecurringLine QuarterlyInApril(string name, decimal amount, int day) =>
        new(Guid.CreateVersion7(), FlowKind.Expense, name, amount, Frequency.Quarterly, day, LongAgo, anchorMonth: 4);

    /// <summary>A quarterly line, due in April only when its anchor month is 1, 4, 7 or 10.</summary>
    public static RecurringLine Quarterly(
        string name, decimal amount, int day, int anchorMonth, SubscriptionInfo? subscription = null, FlowKind kind = FlowKind.Expense) =>
        new(Guid.CreateVersion7(), kind, name, amount, Frequency.Quarterly, day, LongAgo, anchorMonth, subscription: subscription);

    public static RecurringLine Yearly(string name, decimal amount, int day, int anchorMonth) =>
        new(Guid.CreateVersion7(), FlowKind.Expense, name, amount, Frequency.Yearly, day, LongAgo, anchorMonth);

    public static RecurringLine Subscription(string name, decimal amount, int day, DateOnly? commitmentEnd = null) =>
        new(Guid.CreateVersion7(), FlowKind.Expense, name, amount, Frequency.Monthly, day, LongAgo,
            category: Category.Leisure, subscription: new SubscriptionInfo(commitmentEnd));

    public static FinanceMonth April2026(
        decimal startingBalance,
        IEnumerable<RecurringLine> lines,
        IEnumerable<Budget>? budgets = null,
        IEnumerable<BudgetActual>? actuals = null) =>
        new(new MonthPlan(April, startingBalance), lines, budgets ?? [], actuals ?? [], []);
}
