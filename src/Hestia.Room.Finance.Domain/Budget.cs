namespace Hestia.Room.Finance.Domain;

/// <summary>A planned monthly amount for a variable expense item: groceries, outings.</summary>
public sealed class Budget
{
    public Budget(Guid id, string name, decimal plannedAmount)
    {
        Guard.ThrowIfEmpty(id);
        var validName = Guard.ValidName(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plannedAmount);
        Guard.ThrowIfNotCents(plannedAmount);
        Guard.ThrowIfTooLarge(plannedAmount);

        Id = id;
        Name = validName;
        PlannedAmount = plannedAmount;
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>Planned amount in euros for one month, always positive.</summary>
    public decimal PlannedAmount { get; }
}
