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
    }

    public YearMonth Month => Plan.Month;

    public MonthPlan Plan { get; }

    public IReadOnlyList<RecurringLine> Lines { get; }

    public IReadOnlyList<Budget> Budgets { get; }

    public IReadOnlyList<BudgetActual> BudgetActuals { get; }

    public IReadOnlyList<RecurringOverride> Overrides { get; }

    public MonthSummary PlannedSummary() => MonthSummary.Planned(Month, Lines, Budgets);

    public MonthSummary ActualSummary() => MonthSummary.Actual(Month, Lines, Budgets, BudgetActuals, Overrides);

    public CashFlowForecast Forecast() => CashFlowForecast.Compute(Month, Plan.StartingBalance, Lines, Budgets);
}
