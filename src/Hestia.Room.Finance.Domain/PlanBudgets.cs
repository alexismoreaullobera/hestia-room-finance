namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The budgets of the plan as a whole: the rules that involve several budgets, outside of any month (a budget has no
/// dates). Each method gives the budget to save, or throws: the caller writes nothing on a refusal.
/// </summary>
public sealed class PlanBudgets(IEnumerable<Budget> budgets)
{
    private readonly IReadOnlyList<Budget> _budgets = budgets.ToList().AsReadOnly();

    /// <summary>A new budget, under a name no other budget uses.</summary>
    public Budget Create(Guid id, string name, decimal plannedAmount)
    {
        var budget = new Budget(id, name, plannedAmount);
        EnsureNameFree(budget.Name, except: null);
        return budget;
    }

    /// <summary>An existing budget renamed or given another planned amount, everywhere (past months included).</summary>
    public Budget Update(Guid budgetId, string name, decimal plannedAmount)
    {
        EnsureExists(budgetId);
        var budget = new Budget(budgetId, name, plannedAmount);
        // Its own name is free for itself: a change of amount, or of case only, is no conflict.
        EnsureNameFree(budget.Name, except: budgetId);
        return budget;
    }

    /// <summary>Rejects a budget that is not in the plan: before an update or a delete, never silently ignored.</summary>
    public void EnsureExists(Guid budgetId)
    {
        if (_budgets.All(budget => budget.Id != budgetId))
        {
            throw new UnknownBudgetException(nameof(budgetId));
        }
    }

    private void EnsureNameFree(string name, Guid? except)
    {
        if (_budgets.Any(budget => budget.Id != except && Guard.IsSameName(budget.Name, name)))
        {
            throw new NameTakenException(nameof(name));
        }
    }
}
