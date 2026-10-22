using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Infrastructure.Rows;

/// <summary>
/// Explicit conversions between the domain and the database rows. Converting a row to the domain goes through the
/// domain constructors: an invalid row (edited by hand, corrupted) is rejected instead of spreading in the calculations.
/// </summary>
public static class RowMapping
{
    public static RecurringLineRow ToRow(this RecurringLine line) => new()
    {
        Id = line.Id,
        Kind = line.Kind,
        Name = line.Name,
        Amount = line.Amount,
        Frequency = line.Frequency,
        DayOfMonth = line.DayOfMonth,
        AnchorMonth = line.AnchorMonth,
        StartDate = line.StartDate,
        EndDate = line.EndDate,
        Category = line.Category,
        IsSubscription = line.IsSubscription,
        CommitmentEndDate = line.Subscription?.CommitmentEndDate,
    };

    public static RecurringLine ToDomain(this RecurringLineRow row) => new(
        row.Id,
        row.Kind,
        row.Name,
        row.Amount,
        row.Frequency,
        row.DayOfMonth,
        row.StartDate,
        row.AnchorMonth,
        row.EndDate,
        row.Category,
        row.IsSubscription ? new SubscriptionInfo(row.CommitmentEndDate) : null);

    public static BudgetRow ToRow(this Budget budget) => new()
    {
        Id = budget.Id,
        Name = budget.Name,
        PlannedAmount = budget.PlannedAmount,
    };

    public static Budget ToDomain(this BudgetRow row) => new(row.Id, row.Name, row.PlannedAmount);

    public static MonthPlanRow ToRow(this MonthPlan plan) => new()
    {
        Month = plan.Month.FirstDay,
        StartingBalance = plan.StartingBalance,
    };

    public static MonthPlan ToDomain(this MonthPlanRow row) => new(ToYearMonth(row.Month), row.StartingBalance);

    public static BudgetActualRow ToRow(this BudgetActual actual) => new()
    {
        BudgetId = actual.BudgetId,
        Month = actual.Month.FirstDay,
        Amount = actual.Amount,
    };

    public static BudgetActual ToDomain(this BudgetActualRow row) => new(row.BudgetId, ToYearMonth(row.Month), row.Amount);

    public static RecurringOverrideRow ToRow(this RecurringOverride lineOverride) => new()
    {
        LineId = lineOverride.LineId,
        Month = lineOverride.Month.FirstDay,
        ActualAmount = lineOverride.ActualAmount,
    };

    public static RecurringOverride ToDomain(this RecurringOverrideRow row) =>
        row.ActualAmount is { } amount
            ? RecurringOverride.WithAmount(row.LineId, ToYearMonth(row.Month), amount)
            : RecurringOverride.Skipped(row.LineId, ToYearMonth(row.Month));

    /// <summary>A month is stored as its first day: any other day means the row was not written by this code.</summary>
    private static YearMonth ToYearMonth(DateOnly firstDay)
    {
        if (firstDay.Day != 1)
        {
            throw new InvalidOperationException($"A month must be stored as its first day, got {firstDay:yyyy-MM-dd}.");
        }

        return YearMonth.From(firstDay);
    }
}
