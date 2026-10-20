namespace Hestia.Room.Finance.Domain;

public enum FlowKind
{
    Income,
    Expense,
}

public enum Frequency
{
    Monthly,
    Quarterly,
    Yearly,
}

/// <summary>
/// Fixed list of categories, so that views by category stay reliable.
/// User-defined categories may replace it later (see docs/scope.md).
/// </summary>
public enum Category
{
    Other,
    Income,
    Housing,
    Utilities,
    Telecom,
    Insurance,
    Transport,
    Health,
    Food,
    Leisure,
    Education,
    Savings,
    Taxes,
}

/// <summary>Extra information carried by a subscription (a recurring expense).</summary>
public sealed record SubscriptionInfo(DateOnly? CommitmentEndDate = null);

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

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown category.");
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
