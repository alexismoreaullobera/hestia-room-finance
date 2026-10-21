namespace Hestia.Room.Finance.Infrastructure.Rows;

/// <summary>The actual total of a budget for a month. Its identity is the pair (budget, month).</summary>
public sealed class BudgetActualRow
{
    public Guid BudgetId { get; set; }

    /// <summary>The month, stored as its first day.</summary>
    public DateOnly Month { get; set; }

    public decimal Amount { get; set; }
}
