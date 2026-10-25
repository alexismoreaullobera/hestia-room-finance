using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using static Hestia.Room.Finance.Mcp.Tests.Tools.TestMonth;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class PlanOutputTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly Budget Groceries = new(Guid.CreateVersion7(), "Courses", 320m);
    private static readonly Budget Outings = new(Guid.CreateVersion7(), "Sorties", 120.5m);

    [Fact]
    public void Budgets_AreListedByName_WithTheMonthsTheirActualsWereEnteredFor()
    {
        // Given out of order; a fixed identifier lower than any Guid v7, given last, decides the tie of two names.
        var lowSavings = new Budget(new Guid("00000000-0000-0000-0000-000000000001"), "Épargne", 50m);
        var savings = new Budget(Guid.CreateVersion7(), "Épargne", 60m);

        var output = PlanOutput.From(April2026(0m, [], [Outings, savings, Groceries, lowSavings]), new Dictionary<Guid, int> { [Groceries.Id] = 3 });

        Assert.Equal("2026-04", output.Month);
        Assert.Equal(
            [
                new PlanBudgetOutput(Groceries.Id, "Courses", 320m, 3),
                new PlanBudgetOutput(Outings.Id, "Sorties", 120.5m, 0),
                // Ordinal: "Épargne" after "Sorties".
                new PlanBudgetOutput(lowSavings.Id, "Épargne", 50m, 0),
                new PlanBudgetOutput(savings.Id, "Épargne", 60m, 0),
            ],
            output.Budgets);
    }

    [Fact]
    public void Describe_ListsTheBudgets_TheirNamesQuoted()
    {
        var output = PlanOutput.From(April2026(0m, [], [Groceries, Outings]), new Dictionary<Guid, int>());

        Assert.Equal(
            $"Plan pour avril 2026. Budgets : « Courses » {320m.ToString("C", French)} par mois ; « Sorties » {120.5m.ToString("C", French)} par mois.",
            output.Describe());
    }

    [Fact]
    public void Describe_WithoutBudgets_SaysSo()
    {
        Assert.Equal("Plan pour avril 2026 : aucun budget.", PlanOutput.From(April2026(0m, []), new Dictionary<Guid, int>()).Describe());
    }
}
