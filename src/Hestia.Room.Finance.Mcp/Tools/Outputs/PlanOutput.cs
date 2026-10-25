using System.ComponentModel;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.Amounts;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>
/// Structured output of <c>get_plan</c>: the plan itself, as it stands for a month (the recurring lines come in T12).
/// Amounts in euros, rounded to the cent.
/// </summary>
public sealed record PlanOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("The budgets of the plan (variable expenses planned every month), by name.")]
    IReadOnlyList<PlanBudgetOutput> Budgets)
{
    /// <param name="month">The month shown.</param>
    /// <param name="actualsByBudget">For each budget with actuals, the number of months they were entered for.</param>
    public static PlanOutput From(FinanceMonth month, IReadOnlyDictionary<Guid, int> actualsByBudget) => new(
        month.Month.ToString(),
        [.. month.Budgets
            .OrderBy(budget => budget.Name, StringComparer.Ordinal)
            .ThenBy(budget => budget.Id)
            .Select(budget => new PlanBudgetOutput(
                budget.Id, budget.Name, Cents(budget.PlannedAmount), actualsByBudget.GetValueOrDefault(budget.Id)))]);

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe() => Budgets.Count == 0
        ? $"Plan pour {MonthName(Month)} : aucun budget."
        // Names between quotes: a name typed by the user, read by the model, stays a name.
        : $"Plan pour {MonthName(Month)}. Budgets : {string.Join(" ; ", Budgets.Select(b => $"« {b.Name} » {Euros(b.PlannedAmount)} par mois"))}.";
}

/// <summary>A budget of the plan.</summary>
public sealed record PlanBudgetOutput(
    Guid Id,
    string Name,
    [property: Description("Planned amount for every month.")] decimal PlannedAmount,
    [property: Description("Number of months an actual total was entered for: what deleting the budget would also delete.")]
    int ActualsEntered);
