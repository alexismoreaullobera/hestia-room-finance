using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools;
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
    public void Budgets_AreListedByName_WithTheActualWhenEntered()
    {
        var outings = new Budget(Guid.CreateVersion7(), "Sorties", 120.5m);
        // Given out of order, as a database read without ORDER BY would.
        var output = MonthSummaryOutput.From(April2026(0m, [], [outings, Groceries], [new BudgetActual(Groceries.Id, April, 280m)]));

        Assert.Equal(
            [
                new BudgetActualOutput(Groceries.Id, "Courses", 300m, 280m, Entered: true),
                // Without an actual: spent as planned.
                new BudgetActualOutput(outings.Id, "Sorties", 120.5m, 120.5m, Entered: false),
            ],
            output.Budgets);
    }

    [Fact]
    public void Budgets_WithAnActualOfZero_AreEntered()
    {
        // Nothing spent: 0 €, which is not "as planned".
        var output = MonthSummaryOutput.From(April2026(0m, [], [Groceries], [new BudgetActual(Groceries.Id, April, 0m)]));

        Assert.Equal(new BudgetActualOutput(Groceries.Id, "Courses", 300m, 0m, Entered: true), Assert.Single(output.Budgets));
    }

    [Fact]
    public void Lines_AreTheLinesDueThisMonth_ByDate_WithWhatHappened()
    {
        var salary = Monthly(FlowKind.Income, "Salaire", 2000m, day: 28);
        var rent = Monthly(FlowKind.Expense, "Loyer", 650m, day: 5);
        var water = Monthly(FlowKind.Expense, "Eau", 45m, day: 12);
        var bonus = Quarterly("Prime", 300m, day: 30, anchorMonth: 5, kind: FlowKind.Income); // not due in April

        var output = MonthSummaryOutput.From(April2026(0m, [salary, rent, water, bonus],
            overrides: [RecurringOverride.WithAmount(water.Id, April, 52.4m), RecurringOverride.Skipped(rent.Id, April)]));

        Assert.Equal(
            [
                new LineActualOutput(rent.Id, "Loyer", FlowKind.Expense, new DateOnly(2026, 4, 5), 650m, 0m, LineActualStatus.Skipped),
                new LineActualOutput(water.Id, "Eau", FlowKind.Expense, new DateOnly(2026, 4, 12), 45m, 52.4m, LineActualStatus.Corrected),
                new LineActualOutput(salary.Id, "Salaire", FlowKind.Income, new DateOnly(2026, 4, 28), 2000m, 2000m, LineActualStatus.AsPlanned),
            ],
            output.Lines);
        // The same figures as the actual summary: 2 000 € in, 52.40 € out.
        Assert.Equal(52.4m, output.Actual.RecurringExpenses);
    }

    [Fact]
    public void Budgets_AreSortedInFrench_ThenByIdentifier()
    {
        // In French: "Épargne" between "Courses" and "Sorties" (an ordinal order would put it last, É being past every
        // ASCII letter). Two "Courses" by identifier: a fixed one, lower than any Guid v7 and given after the other, so
        // that keeping the input order instead would fail.
        var savings = new Budget(Guid.CreateVersion7(), "Épargne", 50m);
        var outings = new Budget(Guid.CreateVersion7(), "Sorties", 120m);
        var lowGroceries = new Budget(new Guid("00000000-0000-0000-0000-000000000001"), "Courses", 80m);

        var output = MonthSummaryOutput.From(April2026(0m, [], [savings, Groceries, outings, lowGroceries]));

        Assert.Equal([lowGroceries.Id, Groceries.Id, savings.Id, outings.Id], output.Budgets.Select(b => b.Id));
    }

    [Fact]
    public void Describe_NeverLetsANameCloseItsQuote()
    {
        var trap = new Budget(Guid.CreateVersion7(), "Courses » x", 50m);
        var line = Monthly(FlowKind.Expense, "Loyer » y", 650m, day: 5);

        var output = MonthSummaryOutput.From(April2026(0m, [line], [trap],
            actuals: [new BudgetActual(trap.Id, April, 40m)], overrides: [RecurringOverride.Skipped(line.Id, April)]));

        Assert.Contains("« Courses › x »", output.Describe(), StringComparison.Ordinal);
        Assert.Contains("« Loyer › y » : n'a pas eu lieu", output.Describe(), StringComparison.Ordinal);
    }

    [Fact]
    public void Lines_LeaveOutTheOverrideOfALineNotDue()
    {
        // The bonus is not due in April: an override left for it (the line changed since) is not a line of the month.
        var bonus = Quarterly("Prime", 300m, day: 30, anchorMonth: 5, kind: FlowKind.Income);
        var rent = Monthly(FlowKind.Expense, "Loyer", 650m, day: 5);

        var output = MonthSummaryOutput.From(April2026(0m, [bonus, rent], overrides: [RecurringOverride.Skipped(bonus.Id, April)]));

        Assert.Equal([rent.Id], output.Lines.Select(l => l.Id));
        Assert.DoesNotContain("Prime", output.Describe());
    }

    [Fact]
    public void Status_IsText_EvenWithoutTheOptionsOfTheSdk()
    {
        // The SDK writes enums as text only with reflection on; the converter of the type keeps it so under native AOT.
        Assert.Equal("\"Skipped\"", System.Text.Json.JsonSerializer.Serialize(LineActualStatus.Skipped, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
    }

    [Fact]
    public void Describe_NamesTheActualsEntered()
    {
        var water = Monthly(FlowKind.Expense, "Eau", 45m, day: 12);
        var rent = Monthly(FlowKind.Expense, "Loyer", 650m, day: 5);
        var output = MonthSummaryOutput.From(April2026(0m, [water, rent], [Groceries],
            [new BudgetActual(Groceries.Id, April, 280m)],
            [RecurringOverride.WithAmount(water.Id, April, 52m), RecurringOverride.Skipped(rent.Id, April)]));

        var text = output.Describe();

        // The names between quotes: a name typed by the user never blends into the sentence the model reads.
        Assert.Contains($"Réels saisis : « Courses » {280m.ToString("C", French)} pour {300m.ToString("C", French)} prévus", text);
        Assert.Contains($"« Eau » {52m.ToString("C", French)} pour {45m.ToString("C", French)} prévus", text);
        Assert.Contains("« Loyer » : n'a pas eu lieu", text);
    }

    [Fact]
    public void Describe_WithoutActuals_SaysEverythingIsAsPlanned()
    {
        var output = MonthSummaryOutput.From(April2026(0m, [Monthly(FlowKind.Expense, "Loyer", 650m, day: 5)], [Groceries]));

        Assert.EndsWith("Aucun réel saisi : tout est compté comme prévu.", output.Describe());
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
