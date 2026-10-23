using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Infrastructure.Rows;

/// <summary>
/// A recurring line as stored in the database: a plain class with settable properties, easy for EF Core to map.
/// Validity is not checked here: converting it to the domain (<see cref="RowMapping"/>) does.
/// </summary>
public sealed class RecurringLineRow
{
    public Guid Id { get; set; }

    public FlowKind Kind { get; set; }

    public string Name { get; set; } = "";

    public decimal Amount { get; set; }

    public Frequency Frequency { get; set; }

    public int DayOfMonth { get; set; }

    public int? AnchorMonth { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public Category Category { get; set; }

    /// <summary>The domain's optional SubscriptionInfo, flattened: a flag plus its fields.</summary>
    public bool IsSubscription { get; set; }

    public DateOnly? CommitmentEndDate { get; set; }
}
