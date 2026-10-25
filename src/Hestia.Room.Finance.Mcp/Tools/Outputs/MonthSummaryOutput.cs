using System.ComponentModel;
using System.Text.Json.Serialization;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.Amounts;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Structured output of <c>get_month_summary</c>. Amounts in euros, rounded to the cent.</summary>
public sealed record MonthSummaryOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("From the plan: recurring lines and planned budgets.")] SummaryFigures Planned,
    [property: Description("With the actual figures entered for the month (budget actuals, overrides of recurring lines), the plan "
        + "elsewhere. Its smoothed incomes and recurring expenses stay those of the plan; its smoothed savings capacity uses the "
        + "actual budgets.")]
    SummaryFigures Actual,
    [property: Description("The budgets of the month, by name, with their actual total when one was entered.")]
    IReadOnlyList<BudgetActualOutput> Budgets,
    [property: Description("The recurring lines due this month, by date, with what really happened.")]
    IReadOnlyList<LineActualOutput> Lines)
{
    public static MonthSummaryOutput From(FinanceMonth month)
    {
        // Computed first: the actual summary rejects two actuals or overrides of the same budget or line.
        var actual = SummaryFigures.From(month.ActualSummary());
        var actualByBudget = month.BudgetActuals.ToDictionary(a => a.BudgetId);
        var overrideByLine = month.Overrides.ToDictionary(o => o.LineId);

        return new MonthSummaryOutput(
            month.Month.ToString(),
            SummaryFigures.From(month.PlannedSummary()),
            actual,
            [.. month.Budgets
                .OrderBy(budget => budget.Name, NameOrder)
                .ThenBy(budget => budget.Id)
                .Select(budget => BudgetActualOutput.From(budget, actualByBudget.GetValueOrDefault(budget.Id)))],
            [.. month.Occurrences.Select(occurrence => LineActualOutput.From(occurrence, overrideByLine.GetValueOrDefault(occurrence.Line.Id)))]);
    }

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe()
    {
        var entered = Budgets.Where(b => b.Entered).Select(b => b.Describe())
            .Concat(Lines.Where(l => l.Status != LineActualStatus.AsPlanned).Select(l => l.Describe()))
            .ToList();
        var actuals = entered.Count == 0
            ? "Aucun réel saisi : tout est compté comme prévu."
            : $"Réels saisis : {string.Join(" ; ", entered)}.";
        return $"Synthèse pour {MonthName(Month)}. Prévu : {Planned.Describe()}. Réel : {Actual.Describe()}. {actuals}";
    }
}

/// <summary>A budget of the month: planned, and what was really spent when the user entered it.</summary>
public sealed record BudgetActualOutput(
    Guid Id,
    string Name,
    [property: Description("Planned amount for the month.")] decimal Planned,
    [property: Description("The actual total entered, or the planned amount when none was (spent as planned).")]
    decimal Actual,
    [property: Description("True when an actual total was entered for this month.")] bool Entered)
{
    public static BudgetActualOutput From(Budget budget, BudgetActual? actual) => new(
        budget.Id,
        budget.Name,
        Cents(budget.PlannedAmount),
        Cents(actual?.Amount ?? budget.PlannedAmount),
        actual is not null);

    internal string Describe() => $"{Quoted(Name)} {Euros(Actual)} pour {Euros(Planned)} prévus";
}

/// <remarks>Enums as text, by explicit converters (see <see cref="RecurringLineOutput"/>).</remarks>
public sealed record LineActualOutput(
    Guid Id,
    string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<FlowKind>))] FlowKind Kind,
    [property: Description("Date it falls due this month.")] DateOnly Date,
    [property: Description("Planned amount.")] decimal Planned,
    [property: Description("The amount that really happened: the planned one, the corrected one, or 0 when skipped.")]
    decimal Actual,
    [property: JsonConverter(typeof(JsonStringEnumConverter<LineActualStatus>))] LineActualStatus Status)
{
    public static LineActualOutput From(Occurrence occurrence, RecurringOverride? lineOverride)
    {
        var line = occurrence.Line;
        var (status, actual) = lineOverride switch
        {
            null => (LineActualStatus.AsPlanned, line.Amount),
            { ActualAmount: { } amount } => (LineActualStatus.Corrected, amount),
            _ => (LineActualStatus.Skipped, 0m),
        };
        return new LineActualOutput(line.Id, line.Name, line.Kind, occurrence.Date, Cents(line.Amount), Cents(actual), status);
    }

    // Names between quotes: a name typed by the user, read by the model, stays a name and never blends into the sentence.
    internal string Describe() => Status == LineActualStatus.Skipped
        ? $"{Quoted(Name)} : n'a pas eu lieu"
        : $"{Quoted(Name)} {Euros(Actual)} pour {Euros(Planned)} prévus";
}

/// <summary>
/// One reading of the month. Cash: what falls due this month (a yearly insurance weighs fully in its month).
/// Smoothed: quarterly and yearly lines as monthly equivalents, the savings capacity of an average month.
/// </summary>
public sealed record SummaryFigures(
    [property: Description("Incomes falling due this month.")] decimal Incomes,
    [property: Description("Recurring expenses falling due this month.")] decimal RecurringExpenses,
    [property: Description("Total of the budgets (variable expenses such as groceries).")] decimal Budgets,
    [property: Description("Incomes minus recurring expenses minus budgets: what is left at the end of the month.")]
    decimal SavingsCapacity,
    [property: Description("Incomes as monthly equivalents.")] decimal SmoothedIncomes,
    [property: Description("Recurring expenses as monthly equivalents (a 360 € yearly line weighs 30 €).")]
    decimal SmoothedRecurringExpenses,
    [property: Description("Savings capacity of an average month, with the monthly equivalents.")]
    decimal SmoothedSavingsCapacity)
{
    public static SummaryFigures From(MonthSummary summary) => new(
        Cents(summary.CashIncomes),
        Cents(summary.CashRecurringExpenses),
        Cents(summary.BudgetsTotal),
        Cents(summary.CashSavingsCapacity),
        Cents(summary.SmoothedIncomes),
        Cents(summary.SmoothedRecurringExpenses),
        Cents(summary.SmoothedSavingsCapacity));

    internal string Describe() =>
        $"entrées {Euros(Incomes)}, dépenses récurrentes {Euros(RecurringExpenses)}, budgets {Euros(Budgets)}, "
        + $"capacité d'épargne {Euros(SavingsCapacity)} "
        + $"(sur un mois moyen, en lissant les lignes trimestrielles et annuelles : {Euros(SmoothedSavingsCapacity)})";
}
