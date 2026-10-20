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

    /// <summary>
    /// The actual summary of a month: the plan corrected by what really happened.
    /// <list type="bullet">
    /// <item>A recurring line without override is considered paid as planned.</item>
    /// <item>A budget without actual is considered spent as planned (the month may not be over yet).</item>
    /// <item>Actuals and overrides of other months, or of a line not due this month, are ignored.</item>
    /// </list>
    /// The smoothed reading stays the planned one: smoothing is a planning view.
    /// </summary>
    public static MonthSummary Actual(
        YearMonth month,
        IEnumerable<RecurringLine> lines,
        IEnumerable<Budget> budgets,
        IEnumerable<BudgetActual> budgetActuals,
        IEnumerable<RecurringOverride> overrides)
    {
        var allLines = lines.ToList();
        var allBudgets = budgets.ToList();
        var actualByBudget = UniqueByKey(budgetActuals.Where(a => a.Month == month), a => a.BudgetId, nameof(budgetActuals));
        var overrideByLine = UniqueByKey(overrides.Where(o => o.Month == month), o => o.LineId, nameof(overrides));
        var planned = Planned(month, allLines, allBudgets);

        var occurrences = allLines
            .Select(line => line.OccurrenceIn(month))
            .OfType<Occurrence>()
            .Select(o => (o.Line.Kind, Amount: ActualAmountOf(o.Line, overrideByLine)))
            .ToList();

        return planned with
        {
            Incomes = occurrences.Where(o => o.Kind == FlowKind.Income).Sum(o => o.Amount),
            RecurringExpenses = occurrences.Where(o => o.Kind == FlowKind.Expense).Sum(o => o.Amount),
            Budgets = allBudgets.Sum(b => actualByBudget.TryGetValue(b.Id, out var actual) ? actual.Amount : b.PlannedAmount),
        };
    }

    private static decimal ActualAmountOf(RecurringLine line, Dictionary<Guid, RecurringOverride> overrideByLine) =>
        overrideByLine.TryGetValue(line.Id, out var correction)
            ? correction.ActualAmount ?? 0m
            : line.Amount;

    private static Dictionary<Guid, T> UniqueByKey<T>(IEnumerable<T> items, Func<T, Guid> key, string paramName)
    {
        var result = new Dictionary<Guid, T>();
        foreach (var item in items)
        {
            if (!result.TryAdd(key(item), item))
            {
                throw new ArgumentException($"Two entries for {key(item)} in the same month.", paramName);
            }
        }

        return result;
    }
}
