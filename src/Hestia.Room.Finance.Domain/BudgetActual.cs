namespace Hestia.Room.Finance.Domain;

/// <summary>What was really spent on a budget during a month, entered once at the end of the month.</summary>
public sealed record BudgetActual
{
    public BudgetActual(Guid budgetId, YearMonth month, decimal amount)
    {
        Guard.ThrowIfEmpty(budgetId);
        // Zero is allowed: nothing was spent on that budget.
        ArgumentOutOfRangeException.ThrowIfNegative(amount);

        BudgetId = budgetId;
        Month = month;
        Amount = amount;
    }

    public Guid BudgetId { get; }

    public YearMonth Month { get; }

    public decimal Amount { get; }
}
