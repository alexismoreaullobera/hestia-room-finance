using System.Runtime.CompilerServices;

namespace Hestia.Room.Finance.Domain;

/// <summary>
/// An income or an expense that repeats: salary, unemployment benefit, rent, insurance, subscription.
/// Always valid once created: the constructor rejects any inconsistent line.
/// </summary>
public sealed class RecurringLine
{
    public RecurringLine(
        Guid id,
        FlowKind kind,
        string name,
        decimal amount,
        Frequency frequency,
        int dayOfMonth,
        DateOnly startDate,
        int? anchorMonth = null,
        DateOnly? endDate = null,
        Category category = Category.Other,
        SubscriptionInfo? subscription = null)
    {
        // Enums are checked too: any int can be cast to an enum (from storage, or from a tool called by an LLM).
        ThrowIfUndefined(kind);
        ThrowIfUndefined(frequency);
        ThrowIfUndefined(category);
        Guard.ThrowIfEmpty(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        // The sign comes from the kind: an amount is always positive.
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
        ArgumentOutOfRangeException.ThrowIfLessThan(dayOfMonth, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(dayOfMonth, 31);

        if (frequency == Frequency.Monthly && anchorMonth is not null)
        {
            throw new ArgumentException("A monthly line has no anchor month.", nameof(anchorMonth));
        }

        if (frequency != Frequency.Monthly && anchorMonth is not (>= 1 and <= 12))
        {
            throw new ArgumentException("A quarterly or yearly line needs an anchor month between 1 and 12.", nameof(anchorMonth));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot be before the start date.", nameof(endDate));
        }

        if (subscription is not null && kind != FlowKind.Expense)
        {
            throw new ArgumentException("Only an expense can be a subscription.", nameof(subscription));
        }

        Id = id;
        Kind = kind;
        Name = name;
        Amount = amount;
        Frequency = frequency;
        DayOfMonth = dayOfMonth;
        StartDate = startDate;
        AnchorMonth = anchorMonth;
        EndDate = endDate;
        Category = category;
        Subscription = subscription;
    }

    public Guid Id { get; }

    public FlowKind Kind { get; }

    public string Name { get; }

    /// <summary>Amount in euros, always positive: the kind gives the direction.</summary>
    public decimal Amount { get; }

    public Frequency Frequency { get; }

    /// <summary>Day of the debit or credit, 1 to 31 (clamped to the last day of shorter months).</summary>
    public int DayOfMonth { get; }

    public DateOnly StartDate { get; }

    /// <summary>Month of the first occurrence in the year, for quarterly and yearly lines only.</summary>
    public int? AnchorMonth { get; }

    public DateOnly? EndDate { get; }

    public Category Category { get; }

    public SubscriptionInfo? Subscription { get; }

    public bool IsSubscription => Subscription is not null;

    /// <summary>
    /// What the line weighs on one month: a 360 € yearly insurance weighs 30 € per month.
    /// Not rounded: rounding to the cent is done when displaying, after any sum.
    /// </summary>
    public decimal MonthlyEquivalent => Frequency switch
    {
        Frequency.Monthly => Amount,
        Frequency.Quarterly => Amount / 3,
        Frequency.Yearly => Amount / 12,
        _ => throw new InvalidOperationException($"Unknown frequency {Frequency}."),
    };

    /// <summary>
    /// True if the line runs during at least part of the month, whether or not it falls due that month
    /// (a yearly insurance runs all year long). Used by the smoothed reading of the monthly summary.
    /// </summary>
    public bool IsActiveIn(YearMonth month) =>
        StartDate <= month.LastDay && (EndDate is null || EndDate >= month.FirstDay);

    /// <summary>
    /// The occurrence of this line in the given month, or null if it is not due that month.
    /// A line falls due at most once a month (monthly, quarterly and yearly frequencies).
    /// </summary>
    public Occurrence? OccurrenceIn(YearMonth month)
    {
        if (!IsDueIn(month.Month))
        {
            return null;
        }

        // Days 29 to 31 fall on the last day of shorter months. Week-ends are ignored: it is a forecast.
        var date = new DateOnly(month.Year, month.Month, Math.Min(DayOfMonth, month.DaysInMonth));

        var isActive = date >= StartDate && (EndDate is null || date <= EndDate);
        return isActive ? new Occurrence(this, date) : null;
    }

    private static void ThrowIfUndefined<TEnum>(TEnum value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"Unknown {typeof(TEnum).Name}.");
        }
    }

    private bool IsDueIn(int month) => Frequency switch
    {
        Frequency.Monthly => true,
        // Due when the gap with the anchor month is a multiple of 3, before or after it in the year
        // (anchor 11: November, February, May, August). A negative gap works too: -9 % 3 == 0.
        Frequency.Quarterly => (month - AnchorMonth!.Value) % 3 == 0,
        Frequency.Yearly => month == AnchorMonth,
        _ => throw new InvalidOperationException($"Unknown frequency {Frequency}."),
    };
}
