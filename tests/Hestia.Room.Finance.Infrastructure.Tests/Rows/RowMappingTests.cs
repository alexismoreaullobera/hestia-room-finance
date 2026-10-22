using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;

namespace Hestia.Room.Finance.Infrastructure.Tests.Rows;

public class RowMappingTests
{
    private static readonly YearMonth April = new(2026, 4);

    [Fact]
    public void RecurringLine_WithEveryField_SurvivesTheRoundTrip()
    {
        var line = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Car insurance", 360.50m, Frequency.Yearly,
            dayOfMonth: 10, startDate: new DateOnly(2024, 3, 1), anchorMonth: 3, endDate: new DateOnly(2027, 2, 28),
            category: Category.Insurance);

        AssertSameLine(line, line.ToRow().ToDomain());
    }

    [Fact]
    public void RecurringLine_Subscription_SurvivesTheRoundTrip()
    {
        var line = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Mobile plan", 15.99m, Frequency.Monthly,
            dayOfMonth: 8, startDate: new DateOnly(2025, 6, 1), category: Category.Telecom,
            subscription: new SubscriptionInfo(new DateOnly(2026, 12, 31)));

        var row = line.ToRow();

        Assert.True(row.IsSubscription);
        Assert.Equal(new DateOnly(2026, 12, 31), row.CommitmentEndDate);
        AssertSameLine(line, row.ToDomain());
    }

    [Fact]
    public void RecurringLine_SubscriptionWithoutCommitmentEnd_StaysASubscription()
    {
        // The flag keeps the subscription even when it has no field set: this is why SubscriptionInfo is flattened.
        var line = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Streaming", 13.49m, Frequency.Monthly,
            dayOfMonth: 20, startDate: new DateOnly(2025, 1, 1), subscription: new SubscriptionInfo());

        var roundTrip = line.ToRow().ToDomain();

        Assert.True(roundTrip.IsSubscription);
        Assert.Null(roundTrip.Subscription!.CommitmentEndDate);
    }

    [Fact]
    public void RecurringLineRow_Invalid_IsRejectedWhenConvertedToTheDomain()
    {
        var row = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Rent", 650m, Frequency.Monthly, 5, new DateOnly(2025, 1, 1)).ToRow();
        row.DayOfMonth = 45; // edited by hand in the database

        Assert.Throws<ArgumentOutOfRangeException>(() => row.ToDomain());
    }

    [Fact]
    public void Budget_SurvivesTheRoundTrip()
    {
        var budget = new Budget(Guid.CreateVersion7(), "Groceries", 212.40m);

        var roundTrip = budget.ToRow().ToDomain();

        Assert.Equal(budget.Id, roundTrip.Id);
        Assert.Equal(budget.Name, roundTrip.Name);
        Assert.Equal(budget.PlannedAmount, roundTrip.PlannedAmount);
    }

    [Fact]
    public void MonthPlan_IsStoredAsTheFirstDayOfTheMonth()
    {
        var plan = new MonthPlan(April, -30m);

        var row = plan.ToRow();

        Assert.Equal(new DateOnly(2026, 4, 1), row.Month);
        Assert.Equal(plan, row.ToDomain());
    }

    [Fact]
    public void BudgetActual_SurvivesTheRoundTrip()
    {
        var actual = new BudgetActual(Guid.CreateVersion7(), April, 235.10m);

        Assert.Equal(actual, actual.ToRow().ToDomain());
    }

    [Fact]
    public void RecurringOverride_WithAmount_SurvivesTheRoundTrip()
    {
        var lineOverride = RecurringOverride.WithAmount(Guid.CreateVersion7(), April, 52m);

        Assert.Equal(lineOverride, lineOverride.ToRow().ToDomain());
    }

    [Fact]
    public void RecurringOverride_Skipped_IsStoredWithoutAmount()
    {
        var lineOverride = RecurringOverride.Skipped(Guid.CreateVersion7(), April);

        var row = lineOverride.ToRow();

        Assert.Null(row.ActualAmount);
        Assert.Equal(lineOverride, row.ToDomain());
    }

    [Fact]
    public void MonthRow_NotOnTheFirstDay_IsRejected()
    {
        var row = new MonthPlanRow { Month = new DateOnly(2026, 4, 15), StartingBalance = 0m };

        Assert.Throws<InvalidOperationException>(() => row.ToDomain());
    }

    private static void AssertSameLine(RecurringLine expected, RecurringLine actual)
    {
        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Kind, actual.Kind);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Amount, actual.Amount);
        Assert.Equal(expected.Frequency, actual.Frequency);
        Assert.Equal(expected.DayOfMonth, actual.DayOfMonth);
        Assert.Equal(expected.AnchorMonth, actual.AnchorMonth);
        Assert.Equal(expected.StartDate, actual.StartDate);
        Assert.Equal(expected.EndDate, actual.EndDate);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Subscription, actual.Subscription);
    }
}
