using System.ComponentModel;
using System.Text.Json.Serialization;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Structured output of <c>list_subscriptions</c>. Amounts in euros, rounded to the cent.</summary>
public sealed record SubscriptionsOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("Subscriptions running during the month, by day of the month.")]
    IReadOnlyList<SubscriptionOutput> Subscriptions,
    [property: Description("What the subscriptions cost on an average month (sum of the monthly equivalents).")]
    decimal MonthlyTotal,
    [property: Description("What the subscriptions cost over a year.")] decimal YearlyTotal)
{
    public static SubscriptionsOutput From(FinanceMonth month)
    {
        var subscriptions = month.Lines.Where(line => line.IsSubscription).ToList();

        return new SubscriptionsOutput(
            month.Month.ToString(),
            [.. subscriptions
                .OrderBy(line => line.DayOfMonth)
                .ThenBy(line => line.Name, StringComparer.Ordinal)
                .Select(line => SubscriptionOutput.From(line, month.Month))],
            // Sums of the exact amounts, rounded once: never a sum of rounded amounts.
            Cents(subscriptions.Sum(line => line.MonthlyEquivalent)),
            Cents(subscriptions.Sum(line => line.YearlyAmount)));
    }

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe()
    {
        if (Subscriptions.Count == 0)
        {
            return $"Aucun abonnement en cours pour {MonthName(Month)}.";
        }

        return $"Abonnements en cours pour {MonthName(Month)} : {Subscriptions.Count}, "
            + $"soit {Euros(MonthlyTotal)} par mois et {Euros(YearlyTotal)} par an. "
            + string.Join(" ; ", Subscriptions.Select(s => s.Describe())) + ".";
    }
}

public sealed record SubscriptionOutput(
    Guid Id,
    string Name,
    [property: Description("Amount of one debit.")] decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Frequency>))] Frequency Frequency,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Category>))] Category Category,
    [property: Description("What it weighs on an average month.")] decimal MonthlyEquivalent,
    [property: Description("What it costs over a year.")] decimal YearlyCost,
    [property: Description("Date it is debited in this month, or null if it is not due this month.")] DateOnly? DueDate,
    [property: Description("End of the commitment period, if any: before it, cancelling may cost a fee.")]
    DateOnly? CommitmentEndDate)
{
    public static SubscriptionOutput From(RecurringLine line, YearMonth month) => new(
        line.Id,
        line.Name,
        Cents(line.Amount),
        line.Frequency,
        line.Category,
        Cents(line.MonthlyEquivalent),
        Cents(line.YearlyAmount),
        line.OccurrenceIn(month)?.Date,
        line.Subscription?.CommitmentEndDate);

    internal string Describe() =>
        $"{Name} {Euros(Amount)} ({FrequencyName(Frequency)}, "
        + (DueDate is { } date ? $"débit le {Day(date)}" : "pas de débit ce mois-ci")
        + (CommitmentEndDate is { } end ? $", engagement jusqu'au {FullDate(end)})" : ")");
}
