using System.ComponentModel;
using System.Text.Json.Serialization;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.Amounts;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>
/// Structured output of <c>get_plan</c>: the plan itself, as it stands for a month: every budget, and the recurring lines
/// that run during the month. Amounts in euros, rounded to the cent.
/// </summary>
public sealed record PlanOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("The budgets of the plan (variable expenses planned every month), by name.")]
    IReadOnlyList<PlanBudgetOutput> Budgets,
    [property: Description("The recurring lines running during the month (due this month or not), incomes first, then by "
        + "day of the month and by name.")]
    IReadOnlyList<PlanLineOutput> Lines)
{
    /// <param name="month">The month shown.</param>
    /// <param name="actualsByBudget">For each budget with actuals, the number of months they were entered for.</param>
    /// <param name="overridesByLine">For each line with overrides, the number of months they were entered for.</param>
    public static PlanOutput From(
        FinanceMonth month, IReadOnlyDictionary<Guid, int> actualsByBudget, IReadOnlyDictionary<Guid, int> overridesByLine) => new(
        month.Month.ToString(),
        [.. month.Budgets
            .OrderBy(budget => budget.Name, NameOrder)
            .ThenBy(budget => budget.Id)
            .Select(budget => new PlanBudgetOutput(
                budget.Id, budget.Name, Cents(budget.PlannedAmount), actualsByBudget.GetValueOrDefault(budget.Id)))],
        [.. month.Lines
            .OrderByDescending(line => line.Kind == FlowKind.Income)
            .ThenBy(line => line.DayOfMonth)
            .ThenBy(line => line.Name, NameOrder)
            .ThenBy(line => line.Id)
            .Select(line => PlanLineOutput.From(line, overridesByLine.GetValueOrDefault(line.Id)))]);

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe()
    {
        // Names quoted: a name typed by the user, read by the model, stays a name.
        var budgets = JoinOrNone(Budgets.Select(b => $"{Quoted(b.Name)} {Euros(b.PlannedAmount)} par mois"), "aucun");
        var incomes = JoinOrNone(Lines.Where(l => l.Kind == FlowKind.Income).Select(l => l.Describe()), "aucune");
        var expenses = JoinOrNone(Lines.Where(l => l.Kind == FlowKind.Expense).Select(l => l.Describe()), "aucune");
        return $"Plan pour {MonthName(Month)}. Budgets : {budgets}. Entrées : {incomes}. Dépenses : {expenses}.";
    }

    private static string JoinOrNone(IEnumerable<string> items, string none) =>
        string.Join(" ; ", items) is { Length: > 0 } joined ? joined : none;
}

/// <summary>A budget of the plan.</summary>
public sealed record PlanBudgetOutput(
    Guid Id,
    string Name,
    [property: Description("Planned amount for every month.")] decimal PlannedAmount,
    [property: Description("Number of months an actual total was entered for: what deleting the budget would also delete.")]
    int ActualsEntered);

/// <summary>A recurring line of the plan, with every field the plan view enters.</summary>
/// <remarks>Enums as text and nullable values always written, as in <see cref="RecurringLineOutput"/>.</remarks>
public sealed record PlanLineOutput(
    Guid Id,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FlowKind>))] FlowKind Kind,
    [property: Description("Amount of one debit or credit, always positive: the kind gives the direction.")] decimal Amount,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Frequency>))] Frequency Frequency,
    [property: Description("Day of the month it falls due, 1 to 31 (29 to 31: the last day of shorter months).")] int DayOfMonth,
    [property: Description("Month of its first occurrence in the year, 1 to 12, for a quarterly or yearly line; null for a monthly one.")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    int? AnchorMonth,
    [property: Description("First day it runs.")] DateOnly StartDate,
    [property: Description("Last day it runs, or null when it has no end.")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateOnly? EndDate,
    [property: JsonConverter(typeof(JsonStringEnumConverter<Category>))] Category Category,
    [property: Description("True for a subscription (an expense only).")] bool IsSubscription,
    [property: Description("End of the commitment of a subscription, or null (none, or not a subscription).")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    DateOnly? CommitmentEndDate,
    [property: Description("Number of months a correction was entered for: what deleting the line would also delete.")]
    int OverridesEntered)
{
    public static PlanLineOutput From(RecurringLine line, int overridesEntered) => new(
        line.Id,
        line.Name,
        line.Kind,
        Cents(line.Amount),
        line.Frequency,
        line.DayOfMonth,
        line.AnchorMonth,
        line.StartDate,
        line.EndDate,
        line.Category,
        line.IsSubscription,
        line.Subscription?.CommitmentEndDate,
        overridesEntered);

    internal string Describe() =>
        $"{Quoted(Name)} {Euros(Amount)} ({FrequencyName(Frequency)}, le {DayOfMonth}"
        + (EndDate is { } end ? $", jusqu'au {FullDate(end)})" : ")");
}
