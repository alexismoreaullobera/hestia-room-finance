using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using static Hestia.Room.Finance.Mcp.Tests.Tools.TestMonth;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class MonthSummaryOutputTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    private static readonly Budget Groceries = new(Guid.CreateVersion7(), "Courses", 300m);

    // Salary 2 000 €, a 300 € quarterly bonus not due in April (100 € a month smoothed), rent 650 €, a 100 € quarterly
    // insurance due in April, groceries planned 300 €, 280 € spent. Every figure differs from its smoothed reading.
    private static readonly MonthSummaryOutput Output = MonthSummaryOutput.From(April2026(
        startingBalance: 0m,
        lines:
        [
            Monthly(FlowKind.Income, "Salaire", 2000m, day: 28),
            Quarterly("Prime", 300m, day: 30, anchorMonth: 5, kind: FlowKind.Income),
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
    public void SmoothedFigures_UseTheMonthlyEquivalents_RoundedToTheCent()
    {
        // The bonus is not due in April but weighs 100 € on an average month.
        Assert.Equal(2100m, Output.Planned.SmoothedIncomes);
        // The domain keeps 650 + 33.333… €: the output shows cents.
        Assert.Equal(683.33m, Output.Planned.SmoothedRecurringExpenses);
        Assert.Equal(1116.67m, Output.Planned.SmoothedSavingsCapacity);
    }

    [Fact]
    public void HalfCents_AreRoundedAwayFromZero()
    {
        // 30.06 € a year: 2.505 € a month. Away from zero: 2.51 € (the banker's rounding would give 2.50 €),
        // and -2.51 € for the savings capacity, negative.
        var output = MonthSummaryOutput.From(April2026(0m, [Yearly("Antivirus", 30.06m, day: 10, anchorMonth: 9)]));

        Assert.Equal(2.51m, output.Planned.SmoothedRecurringExpenses);
        Assert.Equal(-2.51m, output.Planned.SmoothedSavingsCapacity);
    }

    [Fact]
    public void Describe_IsASelfSufficientSummaryInFrench()
    {
        var text = Output.Describe();

        Assert.StartsWith("Synthèse pour avril 2026.", text);
        Assert.Contains($"capacité d'épargne {950m.ToString("C", French)}", text);
        Assert.Contains($"budgets {280m.ToString("C", French)}", text);
        Assert.Contains(1116.67m.ToString("C", French), text);
    }
}
