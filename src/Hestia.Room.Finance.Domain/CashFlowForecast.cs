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
