namespace Hestia.Room.Finance.Domain;

/// <summary>A recurring line falling due on a given date. Computed, never stored.</summary>
public sealed record Occurrence(RecurringLine Line, DateOnly Date)
{
    /// <summary>The amount with its direction: positive for an income, negative for an expense.</summary>
    public decimal SignedAmount => Line.Kind == FlowKind.Income ? Line.Amount : -Line.Amount;
}
