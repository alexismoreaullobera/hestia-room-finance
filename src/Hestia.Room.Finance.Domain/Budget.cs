namespace Hestia.Room.Finance.Domain;

/// <summary>A planned monthly amount for a variable expense item: groceries, outings.</summary>
public sealed class Budget
{
    public Budget(Guid id, string name, decimal plannedAmount)
    {
        Guard.ThrowIfEmpty(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(plannedAmount);
        Guard.ThrowIfNotCents(plannedAmount);

        Id = id;
        Name = name;
        PlannedAmount = plannedAmount;
    }

    public Guid Id { get; }

    public string Name { get; }

    /// <summary>Planned amount in euros for one month, always positive.</summary>
    public decimal PlannedAmount { get; }
}
