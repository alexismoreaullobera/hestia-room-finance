namespace Hestia.Room.Finance.Domain;

/// <summary>
/// Cash flow forecast of a month (prévisionnel de trésorerie): the expected balance of the account,
/// day by day, from the planned incomes and expenses. Immutable: every reading is computed once.
/// </summary>
public sealed class CashFlowForecast
{
    private CashFlowForecast(
        YearMonth month,
        decimal startingBalance,
        IReadOnlyList<Occurrence> occurrences,
        IReadOnlyList<DailyBalance> days,
        DailyBalance lowPoint,
        IReadOnlyList<Occurrence> contributingDebits)
    {
        Month = month;
        StartingBalance = startingBalance;
        Occurrences = occurrences;
        Days = days;
        LowPoint = lowPoint;
        ContributingDebits = contributingDebits;
    }

    public YearMonth Month { get; }

    /// <summary>Balance assumed at the start of the month (0 € by default in the plan).</summary>
    public decimal StartingBalance { get; }

    /// <summary>The recurring lines falling due this month, by date.</summary>
    public IReadOnlyList<Occurrence> Occurrences { get; }

    /// <summary>One end-of-day balance per day of the month, from the 1st to the last day.</summary>
    public IReadOnlyList<DailyBalance> Days { get; }

    /// <summary>Low point (point bas): the lowest end-of-day balance of the month, the earliest day if several.</summary>
    public DailyBalance LowPoint { get; }

    /// <summary>
    /// Cash buffer (coussin de trésorerie): the minimum to have on the account at the start of the month so the
    /// forecast never goes below zero. The need of the month itself: it does not depend on the starting balance.
    /// </summary>
    public decimal CashBuffer => Math.Max(0m, StartingBalance - LowPoint.Balance);

    /// <summary>True when the forecast, starting from the starting balance, goes below zero.</summary>
    public bool HasAlert => LowPoint.Balance < 0m;

    /// <summary>
    /// The recurring expenses that dig the low point, so the user sees which debit to move: the expenses of the
    /// drawdown, between the last peak before the low point and the low point itself. Empty when the month needs
    /// no buffer. Budgets are not listed: they have no date and weigh evenly every day.
    /// </summary>
    public IReadOnlyList<Occurrence> ContributingDebits { get; }

    /// <summary>
    /// Builds the daily series:
    /// <list type="bullet">
    /// <item>recurring lines on their due date (incomes and expenses of a same day are netted: end-of-day balance);</item>
    /// <item>budgets spread evenly over the days of the month.</item>
    /// </list>
    /// </summary>
    public static CashFlowForecast Compute(YearMonth month, decimal startingBalance, IEnumerable<RecurringLine> lines, IEnumerable<Budget> budgets)
    {
        var occurrences = lines.OccurrencesIn(month);
        var flowsByDate = occurrences.ToLookup(o => o.Date);
        var dailyBudgetShares = budgets.Select(b => SpreadOverMonth(b.PlannedAmount, month.DaysInMonth)).ToList();

        var days = new List<DailyBalance>(month.DaysInMonth);
        var balance = startingBalance;
        for (var day = 1; day <= month.DaysInMonth; day++)
        {
            var date = new DateOnly(month.Year, month.Month, day);
            balance += flowsByDate[date].Sum(o => o.SignedAmount);
            balance -= dailyBudgetShares.Sum(shares => shares[day - 1]);
            days.Add(new DailyBalance(date, balance));
        }

        // A month has at least 28 days: MinBy never returns null here.
        var lowPoint = days.MinBy(d => d.Balance)!;
        var needsBuffer = startingBalance - lowPoint.Balance > 0m;
        IReadOnlyList<Occurrence> contributingDebits = needsBuffer
            ? DrawdownExpenses(occurrences, days, startingBalance, lowPoint)
            : [];

        return new CashFlowForecast(month, startingBalance, occurrences, days.AsReadOnly(), lowPoint, contributingDebits);
    }

    /// <summary>
    /// The expenses between the last peak before the low point and the low point. The peak is the highest balance
    /// reached before the low point, the starting balance counting as the very first one; on equal balances, the
    /// latest day is kept (the drawdown starts when the balance leaves its peak for good).
    /// </summary>
    private static IReadOnlyList<Occurrence> DrawdownExpenses(
        IReadOnlyList<Occurrence> occurrences,
        IReadOnlyList<DailyBalance> days,
        decimal startingBalance,
        DailyBalance lowPoint)
    {
        // Null while the peak is the starting balance, before the first day: not the day before the month, which does
        // not exist for January of the year 1.
        DateOnly? peakDate = null;
        var peakBalance = startingBalance;
        foreach (var day in days.TakeWhile(day => day.Date < lowPoint.Date))
        {
            if (day.Balance >= peakBalance)
            {
                peakBalance = day.Balance;
                peakDate = day.Date;
            }
        }

        return occurrences
            .Where(o => o.Line.Kind == FlowKind.Expense && (peakDate is null || o.Date > peakDate) && o.Date <= lowPoint.Date)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Splits an amount over the days of the month with the largest remainder method: every day gets the share
    /// rounded down to the cent, then the remaining cents go one by one to the first days. The shares add up
    /// exactly to the amount, are never negative, and differ by one cent at most
    /// (200 € over 30 days: 20 days at 6.67 and 10 days at 6.66).
    /// </summary>
    private static decimal[] SpreadOverMonth(decimal amount, int daysInMonth)
    {
        var baseShare = Math.Floor(amount * 100 / daysInMonth) / 100;
        var shares = Enumerable.Repeat(baseShare, daysInMonth).ToArray();

        // Amounts are in cents (see Guard.ThrowIfNotCents): the remainder is a whole number of cents, below one per day.
        var extraCents = (int)((amount - (baseShare * daysInMonth)) * 100);
        for (var i = 0; i < extraCents; i++)
        {
            shares[i] += 0.01m;
        }

        return shares;
    }
}
