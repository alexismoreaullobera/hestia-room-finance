namespace Hestia.Room.Finance.Infrastructure.Rows;

/// <summary>A budget as stored in the database.</summary>
public sealed class BudgetRow
{
    public Guid Id { get; set; }

    public string Name { get; set; } = "";

    public decimal PlannedAmount { get; set; }
}
