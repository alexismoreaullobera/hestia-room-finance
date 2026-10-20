namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The summary of a month, in two readings:
/// <list type="bullet">
/// <item><b>Cash</b>: what really falls due this month (a yearly insurance weighs fully in its month).</item>
/// <item><b>Smoothed</b>: with monthly equivalents (a 360 € yearly insurance weighs 30 € every month),
/// the true savings capacity whatever the month looked at.</item>
/// </list>
/// Amounts are not rounded: rounding is done when displaying.
/// </summary>
public sealed record MonthSummary(
    YearMonth Month,
    decimal Incomes,
    decimal RecurringExpenses,
    decimal Budgets,
    decimal SmoothedIncomes,
    decimal SmoothedRecurringExpenses)
{
    /// <summary>What is left at the end of the month once every expense is paid (can be negative).</summary>
    public decimal SavingsCapacity => Incomes - RecurringExpenses - Budgets;

    public decimal SmoothedSavingsCapacity => SmoothedIncomes - SmoothedRecurringExpenses - Budgets;

    /// <summary>The planned summary of a month, from the recurring lines and the budgets of the plan.</summary>
    public static MonthSummary Planned(YearMonth month, IEnumerable<RecurringLine> lines, IEnumerable<Budget> budgets)
    {
        var allLines = lines.ToList();
        var occurrences = allLines
            .Select(line => line.OccurrenceIn(month))
            .OfType<Occurrence>()
            .ToList();
        var activeLines = allLines.Where(line => line.IsActiveIn(month)).ToList();

        return new MonthSummary(
            month,
            Incomes: occurrences.Where(o => o.Line.Kind == FlowKind.Income).Sum(o => o.Line.Amount),
            RecurringExpenses: occurrences.Where(o => o.Line.Kind == FlowKind.Expense).Sum(o => o.Line.Amount),
            Budgets: budgets.Sum(budget => budget.PlannedAmount),
            SmoothedIncomes: activeLines.Where(l => l.Kind == FlowKind.Income).Sum(l => l.MonthlyEquivalent),
            SmoothedRecurringExpenses: activeLines.Where(l => l.Kind == FlowKind.Expense).Sum(l => l.MonthlyEquivalent));
    }
}
