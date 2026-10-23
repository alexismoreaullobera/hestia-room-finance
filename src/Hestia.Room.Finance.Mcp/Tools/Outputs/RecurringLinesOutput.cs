using System.ComponentModel;
using System.Text.Json.Serialization;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.Amounts;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Structured output of <c>list_recurring_lines</c>. Amounts in euros, rounded to the cent.</summary>
public sealed record RecurringLinesOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("Recurring lines running during the month, incomes first, then by day of the month.")]
    IReadOnlyList<RecurringLineOutput> Lines)
{
    public static RecurringLinesOutput From(FinanceMonth month) => new(
        month.Month.ToString(),
        [.. month.Lines
            .OrderByDescending(line => line.Kind == FlowKind.Income)
            .ThenBy(line => line.DayOfMonth)
            .ThenBy(line => line.Name, StringComparer.Ordinal)
            .Select(line => RecurringLineOutput.From(line, month.Month))]);

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe()
    {
        if (Lines.Count == 0)
        {
            return $"Aucune ligne récurrente pour {MonthName(Month)}.";
        }

        var incomes = Lines.Where(l => l.Kind == FlowKind.Income).Select(l => l.Describe());
        var expenses = Lines.Where(l => l.Kind == FlowKind.Expense).Select(l => l.Describe());
        return $"Lignes récurrentes pour {MonthName(Month)}. "
            + $"Entrées : {JoinOrNone(incomes)}. Dépenses : {JoinOrNone(expenses)}.";
    }

    private static string JoinOrNone(IEnumerable<string> items) =>
        string.Join(" ; ", items) is { Length: > 0 } joined ? joined : "aucune";
}

/// <remarks>
/// Enums are written as text by explicit converters: the SDK default options also do it, but only when reflection is
/// enabled (not with native AOT). The contract read by the agent does not depend on that default.
/// Nullable dates are always written, null included: the SDK default options drop nulls, while the generated output
/// schema lists every property as required.
/// </remarks>
public sealed record RecurringLineOutput(
    Guid Id,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FlowKind>))] FlowKind Kind,
    [property: Description("Amount of one debit or credit.")] decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Frequency>))] Frequency Frequency,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Category>))] Category Category,
    [property: Description("Day of the month it falls due (29 to 31: the last day of shorter months).")] int DayOfMonth,
    [property: Description("Date it falls due in this month, or null if it is not due this month (quarterly or yearly line).")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateOnly? DueDate,
    [property: Description("What it weighs on an average month (a 360 € yearly line weighs 30 €).")] decimal MonthlyEquivalent,
    bool IsSubscription)
{
    public static RecurringLineOutput From(RecurringLine line, YearMonth month) => new(
        line.Id,
        line.Name,
        line.Kind,
        Cents(line.Amount),
        line.Frequency,
        line.Category,
        line.DayOfMonth,
        line.OccurrenceIn(month)?.Date,
        Cents(line.MonthlyEquivalent),
        line.IsSubscription);

    internal string Describe() =>
        $"{Name} {Euros(Amount)} ({FrequencyName(Frequency)}, "
        + (DueDate is { } date ? $"le {Day(date)})" : "pas ce mois-ci)");
}
