namespace Hestia.Room.Finance.Domain;

/// <summary>
/// Everything that describes one month of the plan (starting balance, recurring lines, budgets, actuals and
/// overrides of that month), with the calculations of the month: one complete month, so that no caller assembles
/// the pieces itself.
/// </summary>
public sealed class FinanceMonth
{
    public FinanceMonth(
        MonthPlan plan,
        IEnumerable<RecurringLine> lines,
        IEnumerable<Budget> budgets,
        IEnumerable<BudgetActual> budgetActuals,
        IEnumerable<RecurringOverride> overrides)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Plan = plan;
        Lines = lines.ToList().AsReadOnly();
        Budgets = budgets.ToList().AsReadOnly();
        BudgetActuals = budgetActuals.ToList().AsReadOnly();
        Overrides = overrides.ToList().AsReadOnly();

        // A month only carries its own actuals and overrides: mixing months would be a bug of the caller.
        if (BudgetActuals.Any(a => a.Month != plan.Month) || Overrides.Any(o => o.Month != plan.Month))
        {
            throw new ArgumentException($"Every actual and override must belong to {plan.Month}.");
        }

        Occurrences = Lines.OccurrencesIn(Month);
    }

    public YearMonth Month => Plan.Month;

    public MonthPlan Plan { get; }

    public IReadOnlyList<RecurringLine> Lines { get; }

    public IReadOnlyList<Budget> Budgets { get; }

    public IReadOnlyList<BudgetActual> BudgetActuals { get; }

    public IReadOnlyList<RecurringOverride> Overrides { get; }

    /// <summary>The recurring lines falling due this month, by date: the only ones whose actual amount can be entered.</summary>
    public IReadOnlyList<Occurrence> Occurrences { get; }

    /// <summary>The actual total of a budget for this month, as entered by the user: a budget of this month only.</summary>
    public BudgetActual ActualFor(Guid budgetId, decimal amount)
    {
        EnsureBudget(budgetId);
        return new BudgetActual(budgetId, Month, amount);
    }

    /// <summary>A recurring line due this month that happened with another amount (a 52 € water bill instead of 45 €).</summary>
    public RecurringOverride CorrectionFor(Guid lineId, decimal actualAmount)
    {
        EnsureLineDue(lineId);
        return RecurringOverride.WithAmount(lineId, Month, actualAmount);
    }

    /// <summary>A recurring line due this month that did not happen.</summary>
    public RecurringOverride SkippedFor(Guid lineId)
    {
        EnsureLineDue(lineId);
        return RecurringOverride.Skipped(lineId, Month);
    }

    /// <summary>
    /// Rejects a budget that is not one of this month. Also checked before clearing an actual: an unknown identifier is
    /// a mistake of the caller, never silently ignored.
    /// </summary>
    public void EnsureBudget(Guid budgetId)
    {
        if (Budgets.All(budget => budget.Id != budgetId))
        {
            throw new ArgumentException("No budget with this identifier in the month.", nameof(budgetId));
        }
    }

    /// <summary>
    /// Rejects a recurring line not due this month (not running, or a quarterly or yearly line due another month): its
    /// override would be ignored by the calculations, so it is refused when entered. Also checked before clearing one.
    /// </summary>
    public void EnsureLineDue(Guid lineId)
    {
        if (Occurrences.All(occurrence => occurrence.Line.Id != lineId))
        {
            throw new ArgumentException("No recurring line with this identifier due in the month.", nameof(lineId));
        }
    }

    public MonthSummary PlannedSummary() => MonthSummary.Planned(Month, Lines, Budgets);

    public MonthSummary ActualSummary() => MonthSummary.Actual(Month, Lines, Budgets, BudgetActuals, Overrides);

    public CashFlowForecast Forecast() => CashFlowForecast.Compute(Month, Plan.StartingBalance, Lines, Budgets);
}
