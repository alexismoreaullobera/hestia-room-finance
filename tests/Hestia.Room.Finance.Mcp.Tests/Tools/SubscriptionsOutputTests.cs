using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using static Hestia.Room.Finance.Mcp.Tests.Tools.TestMonth;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class SubscriptionsOutputTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly SubscriptionsOutput Output = SubscriptionsOutput.From(April2026(0m,
    [
        Monthly(FlowKind.Expense, "Loyer", 650m, day: 5),
        Subscription("Streaming", 13.49m, day: 20),
        Subscription("Salle de sport", 34.90m, day: 3, commitmentEnd: new DateOnly(2026, 12, 31)),
    ]));

    [Fact]
    public void Subscriptions_AreOnlyTheSubscriptionLines_ByDayOfTheMonth()
    {
        Assert.Equal(["Salle de sport", "Streaming"], Output.Subscriptions.Select(s => s.Name));
    }

    [Fact]
    public void Subscriptions_OnTheSameDay_AreOrderedByName()
    {
        // Written in the reverse order on purpose: the order comes from the projection, not from the plan.
        var output = SubscriptionsOutput.From(April2026(0m,
        [
            Subscription("Forfait mobile", 12.99m, day: 8),
            Subscription("Box internet", 29.99m, day: 8),
        ]));

        Assert.Equal(["Box internet", "Forfait mobile"], output.Subscriptions.Select(s => s.Name));
    }

    [Fact]
    public void Totals_AreTheMonthlyAndYearlyCosts()
    {
        Assert.Equal(48.39m, Output.MonthlyTotal);
        Assert.Equal(580.68m, Output.YearlyTotal);
        Assert.Equal(161.88m, Output.Subscriptions.Single(s => s.Name == "Streaming").YearlyCost);
    }

    [Fact]
    public void QuarterlySubscriptions_KeepTheirAmount_AndTotalsRoundTheExactSum()
    {
        // Two 10 € quarterly subscriptions: 3.333… € each. Rounded once: 6.67 €; rounded each: 6.66 €.
        var output = SubscriptionsOutput.From(April2026(0m,
        [
            Quarterly("Presse", 10m, day: 1, anchorMonth: 1, new SubscriptionInfo()),
            Quarterly("Magazine", 10m, day: 2, anchorMonth: 1, new SubscriptionInfo()),
        ]));

        Assert.Equal(6.67m, output.MonthlyTotal);
        Assert.Equal(80m, output.YearlyTotal);
        Assert.All(output.Subscriptions, s =>
        {
            Assert.Equal(10m, s.Amount);
            Assert.Equal(3.33m, s.MonthlyEquivalent);
            Assert.Equal(40m, s.YearlyCost);
        });
    }

    [Fact]
    public void Subscription_KeepsItsDebitDateAndItsCommitmentEnd()
    {
        var gym = Output.Subscriptions.Single(s => s.Name == "Salle de sport");

        Assert.Equal(new DateOnly(2026, 4, 3), gym.DueDate);
        Assert.Equal(new DateOnly(2026, 12, 31), gym.CommitmentEndDate);
    }

    [Fact]
    public void Describe_GivesTheTotalsAndEachSubscription()
    {
        var text = Output.Describe();

        Assert.StartsWith(
            $"Abonnements en cours pour avril 2026 : 2, soit {48.39m.ToString("C", French)} par mois et {580.68m.ToString("C", French)} par an.",
            text);
        Assert.Contains("débit le 03/04, engagement jusqu'au 31/12/2026)", text);
        // No commitment: the parenthesis closes right after the debit date.
        Assert.Contains($"Streaming {13.49m.ToString("C", French)} (mensuel, débit le 20/04).", text);
    }

    [Fact]
    public void Describe_ASubscriptionNotDueThisMonth_SaysSo()
    {
        var output = SubscriptionsOutput.From(April2026(0m, [Quarterly("Presse", 10m, day: 1, anchorMonth: 2, new SubscriptionInfo())]));

        Assert.Null(output.Subscriptions.Single().DueDate);
        Assert.Contains("(trimestriel, pas de débit ce mois-ci)", output.Describe());
    }

    [Fact]
    public void Describe_WithoutSubscription_SaysSo()
    {
        var output = SubscriptionsOutput.From(April2026(0m, [Monthly(FlowKind.Expense, "Loyer", 650m, day: 5)]));

        Assert.Empty(output.Subscriptions);
        Assert.Equal("Aucun abonnement en cours pour avril 2026.", output.Describe());
    }
}
