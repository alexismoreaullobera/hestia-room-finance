using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using static Hestia.Room.Finance.Mcp.Tests.Tools.TestMonth;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class MonthSummaryOutputTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly Budget Groceries = new(Guid.CreateVersion7(), "Courses", 300m);

    // Salary 2 000 €, rent 650 €, a 100 € quarterly insurance due in April, groceries planned 300 €, 280 € spent.
    private static readonly MonthSummaryOutput Output = MonthSummaryOutput.From(April2026(
        startingBalance: 0m,
        lines:
        [
            Monthly(FlowKind.Income, "Salaire", 2000m, day: 28),
            Monthly(FlowKind.Expense, "Loyer", 650m, day: 5),
            QuarterlyInApril("Assurance", 100m, day: 15),
        ],
        budgets: [Groceries],
        actuals: [new BudgetActual(Groceries.Id, April, 280m)]));

    [Fact]
    public void Planned_IsTheSummaryOfThePlan()
    {
        Assert.Equal("2026-04", Output.Month);
        Assert.Equal(2000m, Output.Planned.Incomes);
        Assert.Equal(750m, Output.Planned.RecurringExpenses);
        Assert.Equal(300m, Output.Planned.Budgets);
        Assert.Equal(950m, Output.Planned.SavingsCapacity);
    }

    [Fact]
    public void Actual_TakesTheBudgetActualsIntoAccount()
    {
        Assert.Equal(280m, Output.Actual.Budgets);
        Assert.Equal(970m, Output.Actual.SavingsCapacity);
    }

    [Fact]
    public void SmoothedFigures_AreRoundedToTheCent()
    {
        // The domain keeps 650 + 33.333… €: the output shows cents.
        Assert.Equal(683.33m, Output.Planned.SmoothedRecurringExpenses);
        Assert.Equal(1016.67m, Output.Planned.SmoothedSavingsCapacity);
    }

    [Fact]
    public void Describe_IsASelfSufficientSummaryInFrench()
    {
        var text = Output.Describe();

        Assert.StartsWith("Synthèse pour avril 2026.", text);
        Assert.Contains($"capacité d'épargne {950m.ToString("C", French)}", text);
        Assert.Contains($"budgets {280m.ToString("C", French)}", text);
        Assert.Contains(1016.67m.ToString("C", French), text);
    }
}
