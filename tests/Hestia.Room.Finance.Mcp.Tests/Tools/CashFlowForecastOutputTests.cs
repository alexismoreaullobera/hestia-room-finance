using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using static Hestia.Room.Finance.Mcp.Tests.Tools.TestMonth;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class CashFlowForecastOutputTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    // Rent 650 € on the 5th, salary 2 000 € on the 28th: the low point is between the two.
    private static CashFlowForecastOutput Forecast(decimal startingBalance) => CashFlowForecastOutput.From(April2026(
        startingBalance,
        [
            Monthly(FlowKind.Expense, "Loyer", 650m, day: 5),
            Monthly(FlowKind.Income, "Salaire", 2000m, day: 28),
        ]).Forecast());

    [Fact]
    public void From_KeepsTheReadingsOfTheForecast()
    {
        var output = Forecast(startingBalance: 100m);

        Assert.Equal("2026-04", output.Month);
        Assert.Equal(100m, output.StartingBalance);
        Assert.Equal(new DailyBalanceOutput(new DateOnly(2026, 4, 5), -550m), output.LowPoint);
        Assert.Equal(650m, output.CashBuffer);
        Assert.True(output.HasAlert);
        Assert.False(output.StartsOverdrawn);
        Assert.Equal([new DebitOutput("Loyer", new DateOnly(2026, 4, 5), 650m)], output.ContributingDebits);
    }

    [Fact]
    public void From_GivesOneBalancePerDayOfTheMonth()
    {
        var days = Forecast(startingBalance: 100m).Days;

        Assert.Equal(30, days.Count);
        Assert.Equal(new DailyBalanceOutput(new DateOnly(2026, 4, 1), 100m), days[0]);
        Assert.Equal(new DailyBalanceOutput(new DateOnly(2026, 4, 30), 1450m), days[^1]);
    }

    [Fact]
    public void Describe_WithAnAlert_SaysHowMuchToStartWithAndWhichDebitsToMove()
    {
        var text = Forecast(startingBalance: 100m).Describe();

        Assert.StartsWith("Prévisionnel pour avril 2026", text);
        Assert.Contains($"point bas {(-550m).ToString("C", French)} le 05/04", text);
        Assert.Contains("Alerte", text);
        Assert.Contains($"au moins {650m.ToString("C", French)}", text);
        // The name quoted: typed by the user, read by the model.
        Assert.Contains($"« Loyer » ({650m.ToString("C", French)}, le 05/04)", text);
    }

    [Fact]
    public void Describe_WithoutAlert_SaysTheBalanceStaysAboveZero_AndGivesTheBuffer()
    {
        var text = Forecast(startingBalance: 1000m).Describe();

        Assert.Contains("Pas d'alerte : le solde ne passe pas sous zéro", text);
        Assert.Contains($"coussin de trésorerie nécessaire : {650m.ToString("C", French)}", text);
        Assert.DoesNotContain("Alerte :", text);
    }

    [Fact]
    public void Describe_AMonthStartingOverdrawn_WithNothingDiggingDeeper_SaysSoWithoutAnEmptyList()
    {
        // -100 € on the 1st, then only the salary: below zero, but the month itself needs no buffer.
        var output = CashFlowForecastOutput.From(April2026(-100m, [Monthly(FlowKind.Income, "Salaire", 2000m, day: 28)]).Forecast());

        var text = output.Describe();

        Assert.True(output.HasAlert);
        Assert.True(output.StartsOverdrawn);
        Assert.Equal(0m, output.CashBuffer);
        Assert.Contains("Le compte démarre déjà à découvert", text);
        Assert.DoesNotContain("au moins", text);
        Assert.DoesNotContain("Débits qui creusent", text);
    }

    [Fact]
    public void Describe_AnAlertDugByBudgetsOnly_GivesTheBufferWithoutAnEmptyList()
    {
        // Budgets have no date: they dig the balance every day, and are never listed as debits.
        var groceries = new Budget(Guid.CreateVersion7(), "Courses", 300m);
        var output = CashFlowForecastOutput.From(April2026(0m, [], [groceries]).Forecast());

        var text = output.Describe();

        Assert.True(output.HasAlert);
        Assert.Empty(output.ContributingDebits);
        Assert.Contains($"au moins {300m.ToString("C", French)}", text);
        Assert.DoesNotContain("Débits qui creusent", text);
    }

    [Fact]
    public void Describe_AnEmptyMonth_HasNoAlert()
    {
        var output = CashFlowForecastOutput.From(April2026(0m, []).Forecast());

        Assert.False(output.HasAlert);
        Assert.Equal(30, output.Days.Count);
        Assert.Contains("Pas d'alerte", output.Describe());
    }
}
