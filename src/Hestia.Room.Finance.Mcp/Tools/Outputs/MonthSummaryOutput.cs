using System.ComponentModel;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Structured output of <c>get_month_summary</c>. Amounts in euros, rounded to the cent.</summary>
public sealed record MonthSummaryOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("From the plan: recurring lines and planned budgets.")] SummaryFigures Planned,
    [property: Description("With the actual figures entered for the month (budget actuals, overrides of recurring lines), the plan elsewhere.")]
    SummaryFigures Actual)
{
    public static MonthSummaryOutput From(FinanceMonth month) => new(
        month.Month.ToString(),
        SummaryFigures.From(month.PlannedSummary()),
        SummaryFigures.From(month.ActualSummary()));

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe() =>
        $"Synthèse pour {MonthName(Month)}. Prévu : {Planned.Describe()}. Réel : {Actual.Describe()}.";
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
        + $"(sur un mois moyen, en lissant les dépenses trimestrielles et annuelles : {Euros(SmoothedSavingsCapacity)})";
}
