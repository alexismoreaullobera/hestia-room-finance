using System.Globalization;

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

    /// <summary>
    /// Reads a month written as <c>YYYY-MM</c>, the format of <see cref="ToString"/> (e.g. 2026-04).
    /// Strict: exactly four digits, a dash and two digits, nothing around them.
    /// </summary>
    public static bool TryParse(string? text, out YearMonth month)
    {
        month = default;

        // NumberStyles.None: no sign, no space, ASCII digits only.
        if (text is not { Length: 7 } || text[4] != '-'
            || !int.TryParse(text.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(text.AsSpan(5, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var monthNumber)
            || year < 1 || monthNumber is < 1 or > 12)
        {
            return false;
        }

        month = new YearMonth(year, monthNumber);
        return true;
    }
}
