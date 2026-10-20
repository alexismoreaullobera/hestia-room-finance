namespace Hestia.Room.Finance.Domain;

/// <summary>Forecast balance of the account at the end of a day.</summary>
public sealed record DailyBalance(DateOnly Date, decimal Balance);

/// <summary>
/// Cash flow forecast of a month (prévisionnel de trésorerie): the expected balance of the account,
/// day by day, from the planned incomes and expenses.
/// </summary>
public sealed class CashFlowForecast
{
    private CashFlowForecast(YearMonth month, decimal startingBalance, IReadOnlyList<Occurrence> occurrences, IReadOnlyList<DailyBalance> days)
    {
        Month = month;
        StartingBalance = startingBalance;
        Occurrences = occurrences;
        Days = days;
    }

    public YearMonth Month { get; }

    /// <summary>Balance assumed at the start of the month (0 € by default in the plan).</summary>
    public decimal StartingBalance { get; }

    /// <summary>The recurring lines falling due this month, by date.</summary>
    public IReadOnlyList<Occurrence> Occurrences { get; }

    /// <summary>One end-of-day balance per day of the month, from the 1st to the last day.</summary>
    public IReadOnlyList<DailyBalance> Days { get; }

    /// <summary>Low point (point bas): the lowest end-of-day balance of the month, the earliest day if several.</summary>
    public DailyBalance LowPoint => Days.MinBy(day => day.Balance)!;

    /// <summary>
    /// Cash buffer (coussin de trésorerie): the minimum to have on the account at the start of the month so the
    /// forecast never goes below zero. The need of the month itself: it does not depend on the starting balance.
    /// </summary>
    public decimal CashBuffer => Math.Max(0m, StartingBalance - LowPoint.Balance);

    /// <summary>True when the forecast, starting from the starting balance, goes below zero.</summary>
    public bool HasAlert => LowPoint.Balance < 0m;

    /// <summary>
    /// The recurring expenses that dig the low point, so the user sees which debit to move: the expenses of the
    /// drawdown, between the last peak before the low point (the highest balance reached before it, the starting
    /// balance counting as the very first one) and the low point itself. Empty when the month needs no buffer.
    /// Budgets are not listed: they have no date and weigh evenly every day.
    /// </summary>
    public IReadOnlyList<Occurrence> ContributingDebits
    {
        get
        {
            if (CashBuffer == 0m)
            {
                return [];
            }

            var lowPoint = LowPoint;
            // Before day 1, the balance is the starting balance. On equal balances, the latest day is kept:
            // the drawdown starts when the balance leaves its peak for good.
            var peakDate = Month.FirstDay.AddDays(-1);
            var peakBalance = StartingBalance;
            foreach (var day in Days.TakeWhile(day => day.Date < lowPoint.Date))
            {
                if (day.Balance >= peakBalance)
                {
                    peakBalance = day.Balance;
                    peakDate = day.Date;
                }
            }

            return Occurrences
                .Where(o => o.Line.Kind == FlowKind.Expense && o.Date > peakDate && o.Date <= lowPoint.Date)
                .ToList();
        }
    }

    /// <summary>
    /// Builds the daily series:
    /// <list type="bullet">
    /// <item>recurring lines on their due date (incomes and expenses of a same day are netted: end-of-day balance);</item>
    /// <item>budgets spread evenly over the days of the month.</item>
    /// </list>
    /// </summary>
    public static CashFlowForecast Compute(YearMonth month, decimal startingBalance, IEnumerable<RecurringLine> lines, IEnumerable<Budget> budgets)
    {
        var occurrences = lines
            .Select(line => line.OccurrenceIn(month))
            .OfType<Occurrence>()
            .OrderBy(o => o.Date)
            .ToList();
        var dailyBudgetShares = budgets.Select(b => SpreadOverMonth(b.PlannedAmount, month.DaysInMonth)).ToList();

        var days = new List<DailyBalance>(month.DaysInMonth);
        var balance = startingBalance;
        for (var day = 1; day <= month.DaysInMonth; day++)
        {
            var date = new DateOnly(month.Year, month.Month, day);
            balance += occurrences.Where(o => o.Date == date).Sum(SignedAmount);
            balance -= dailyBudgetShares.Sum(shares => shares[day - 1]);
            days.Add(new DailyBalance(date, balance));
        }

        return new CashFlowForecast(month, startingBalance, occurrences, days);
    }

    private static decimal SignedAmount(Occurrence occurrence) =>
        occurrence.Line.Kind == FlowKind.Income ? occurrence.Line.Amount : -occurrence.Line.Amount;

    /// <summary>
    /// Splits an amount over the days of the month: a daily share rounded to the cent, the remainder on the
    /// last day, so that the shares always add up exactly to the amount (200 € over 30 days: 29 × 6.67 + 6.57).
    /// </summary>
    private static decimal[] SpreadOverMonth(decimal amount, int daysInMonth)
    {
        var dailyShare = Math.Round(amount / daysInMonth, 2, MidpointRounding.AwayFromZero);
        var shares = Enumerable.Repeat(dailyShare, daysInMonth).ToArray();
        shares[^1] = amount - (dailyShare * (daysInMonth - 1));
        return shares;
    }
}
