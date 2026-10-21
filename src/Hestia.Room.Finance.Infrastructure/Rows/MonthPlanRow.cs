namespace Hestia.Room.Finance.Infrastructure.Rows;

/// <summary>The settings of a month as stored in the database, one row per month.</summary>
public sealed class MonthPlanRow
{
    /// <summary>The month, stored as its first day (a readable SQL date, unlike a yyyyMM integer).</summary>
    public DateOnly Month { get; set; }

    public decimal StartingBalance { get; set; }
}
