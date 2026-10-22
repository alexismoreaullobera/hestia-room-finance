namespace Hestia.Room.Finance.Domain;

/// <summary>
/// A calendar month (year and month, no day): the unit of the monthly plan.
/// .NET has no such type (DateOnly always carries a day).
/// </summary>
public readonly record struct YearMonth
{
    public YearMonth(int year, int month)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ArgumentOutOfRangeException.ThrowIfLessThan(month, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(month, 12);

        Year = year;
        Month = month;
    }

    public int Year { get; }

    public int Month { get; }

    public int DaysInMonth => DateTime.DaysInMonth(Year, Month);

    public DateOnly FirstDay => new(Year, Month, 1);

    public DateOnly LastDay => new(Year, Month, DaysInMonth);

    public static YearMonth From(DateOnly date) => new(date.Year, date.Month);

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

    /// <summary>The month <paramref name="months"/> months later, or earlier when negative.</summary>
    public YearMonth AddMonths(int months) => From(FirstDay.AddMonths(months));

    public override string ToString() => $"{Year:D4}-{Month:D2}";
}
