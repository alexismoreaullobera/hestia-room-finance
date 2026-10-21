namespace Hestia.Room.Finance.Domain;

/// <summary>
/// A month where a recurring line did not go as planned: a different amount (a 52 € water bill instead of 45 €)
/// or no debit at all. Without an override, a recurring line is considered paid as planned.
/// </summary>
public sealed record RecurringOverride
{
    private RecurringOverride(Guid lineId, YearMonth month, decimal? actualAmount)
    {
        Guard.ThrowIfEmpty(lineId);
        LineId = lineId;
        Month = month;
        ActualAmount = actualAmount;
    }

    public Guid LineId { get; }

    public YearMonth Month { get; }

    /// <summary>The amount that really happened, or null if the line did not happen this month.</summary>
    public decimal? ActualAmount { get; }

    public bool IsSkipped => ActualAmount is null;

    public static RecurringOverride WithAmount(Guid lineId, YearMonth month, decimal actualAmount)
    {
        // A zero amount would be an ambiguous "skipped": Skipped says it explicitly.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(actualAmount);
        Guard.ThrowIfNotCents(actualAmount);
        return new RecurringOverride(lineId, month, actualAmount);
    }

    public static RecurringOverride Skipped(Guid lineId, YearMonth month) => new(lineId, month, null);
}
