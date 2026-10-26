namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The whole plan, outside of any month: every budget and every recurring line, ended and future ones included. What the
/// rules of the plan check a change against (a free name, an existing item), never a month of calculations.
/// </summary>
public sealed class FinancePlan(IEnumerable<Budget> budgets, IEnumerable<RecurringLine> lines)
{
    public PlanBudgets Budgets { get; } = new(budgets);

    public PlanLines Lines { get; } = new(lines);
}
