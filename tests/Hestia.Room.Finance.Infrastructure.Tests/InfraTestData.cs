using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Infrastructure.Tests;

/// <summary>Shared data and assertions of the infrastructure tests, as TestPlan on the domain side.</summary>
internal static class InfraTestData
{
    public static readonly YearMonth April = new(2026, 4);

    /// <summary>A start date far in the past: the line is active in every month of the tests.</summary>
    public static readonly DateOnly LongAgo = new(2025, 1, 1);

    public static CancellationToken Token => TestContext.Current.CancellationToken;

    public static RecurringLine Expense(string name, decimal amount, int day = 2, DateOnly? start = null, DateOnly? end = null) =>
        new(Guid.CreateVersion7(), FlowKind.Expense, name, amount, Frequency.Monthly, day, start ?? LongAgo, endDate: end);

    public static Budget NewBudget(string name, decimal plannedAmount) => new(Guid.CreateVersion7(), name, plannedAmount);

    /// <summary>A RecurringLine has reference equality: compares every field instead.</summary>
    public static void AssertSameLine(RecurringLine expected, RecurringLine actual)
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
