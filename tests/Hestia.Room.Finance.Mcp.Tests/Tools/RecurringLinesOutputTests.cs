using System.Globalization;
using System.Text.Json;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using ModelContextProtocol;
using static Hestia.Room.Finance.Mcp.Tests.Tools.TestMonth;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class RecurringLinesOutputTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly RecurringLinesOutput Output = RecurringLinesOutput.From(April2026(0m,
    [
        Monthly(FlowKind.Expense, "Loyer", 650m, day: 5),
        Monthly(FlowKind.Expense, "Électricité", 68m, day: 12),
        Monthly(FlowKind.Income, "Salaire", 2000m, day: 28),
        // Due in January, April, July and October: due this month.
        Quarterly("Assurance auto", 100m, day: 15, anchorMonth: 1),
        // Due in February, May, August and November: not this month.
        Quarterly("Taxe", 90m, day: 20, anchorMonth: 2),
    ]));

    [Fact]
    public void Lines_ComeIncomesFirstThenByDayOfTheMonth()
    {
        Assert.Equal(["Salaire", "Loyer", "Électricité", "Assurance auto", "Taxe"], Output.Lines.Select(l => l.Name));
    }

    [Fact]
    public void DueDate_IsTheDateInTheMonth_OrNullWhenTheLineIsNotDue()
    {
        Assert.Equal(new DateOnly(2026, 4, 15), Line("Assurance auto").DueDate);
        Assert.Null(Line("Taxe").DueDate);
    }

    [Fact]
    public void MonthlyEquivalent_IsRoundedToTheCent()
    {
        Assert.Equal(33.33m, Line("Assurance auto").MonthlyEquivalent);
        Assert.Equal(100m, Line("Assurance auto").Amount);
    }

    [Fact]
    public void StructuredContent_WritesTheEnumsAsText()
    {
        // As the tool serializes it: the agent reads "Income", never a number.
        var json = JsonSerializer.SerializeToElement(Output, McpJsonUtilities.DefaultOptions);
        var salary = json.GetProperty("lines")[0];

        Assert.Equal("Income", salary.GetProperty("kind").GetString());
        Assert.Equal("Monthly", salary.GetProperty("frequency").GetString());
        Assert.Equal("Other", salary.GetProperty("category").GetString());
    }

    [Fact]
    public void Describe_ListsIncomesAndExpensesWithTheirDate()
    {
        var text = Output.Describe();

        Assert.StartsWith("Lignes récurrentes pour avril 2026.", text);
        Assert.Contains($"Entrées : Salaire {2000m.ToString("C", French)} (mensuel, le 28/04)", text);
        Assert.Contains($"Taxe {90m.ToString("C", French)} (trimestriel, pas ce mois-ci)", text);
    }

    [Fact]
    public void Describe_WithoutLines_SaysSo()
    {
        Assert.Equal("Aucune ligne récurrente pour avril 2026.", RecurringLinesOutput.From(April2026(0m, [])).Describe());
    }

    private static RecurringLineOutput Line(string name) => Output.Lines.Single(l => l.Name == name);
}
