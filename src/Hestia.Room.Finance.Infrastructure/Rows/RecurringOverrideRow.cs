namespace Hestia.Room.Finance.Infrastructure.Rows;

/// <summary>A month where a recurring line did not go as planned. Its identity is the pair (line, month).</summary>
public sealed class RecurringOverrideRow
{
    public Guid LineId { get; set; }

    /// <summary>The month, stored as its first day.</summary>
    public DateOnly Month { get; set; }

    /// <summary>The amount that really happened, or null if the line did not happen this month.</summary>
    public decimal? ActualAmount { get; set; }
}
