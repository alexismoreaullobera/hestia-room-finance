using System.ComponentModel;
using Hestia.Room.Finance.Domain;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.Amounts;
using static Hestia.Room.Finance.Mcp.Tools.Outputs.FrenchText;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Structured output of <c>get_cash_flow_forecast</c>. Amounts in euros, rounded to the cent.</summary>
public sealed record CashFlowForecastOutput(
    [property: Description("Month, as YYYY-MM.")] string Month,
    [property: Description("Balance assumed at the start of the month.")] decimal StartingBalance,
    [property: Description("Lowest end-of-day balance of the month, the earliest day if several.")] DailyBalanceOutput LowPoint,
    [property: Description("Minimum to have on the account at the start of the month so the balance never goes below zero.")]
    decimal CashBuffer,
    [property: Description("True when the balance, from the starting balance, goes below zero during the month.")] bool HasAlert,
    [property: Description("Recurring expenses that dig the low point: the ones to move to avoid it. Empty when the month needs no buffer.")]
    IReadOnlyList<DebitOutput> ContributingDebits,
    [property: Description("End-of-day forecast balance, for every day of the month.")] IReadOnlyList<DailyBalanceOutput> Days)
{
    public static CashFlowForecastOutput From(CashFlowForecast forecast) => new(
        forecast.Month.ToString(),
        Cents(forecast.StartingBalance),
        DailyBalanceOutput.From(forecast.LowPoint),
        Cents(forecast.CashBuffer),
        forecast.HasAlert,
        [.. forecast.ContributingDebits.Select(DebitOutput.From)],
        [.. forecast.Days.Select(DailyBalanceOutput.From)]);

    /// <summary>Self-sufficient text summary, for the model and for hosts without UI.</summary>
    public string Describe()
    {
        var start = $"Prévisionnel pour {MonthName(Month)} : solde de départ {Euros(StartingBalance)}, "
            + $"point bas {Euros(LowPoint.Balance)} le {Day(LowPoint.Date)}.";

        if (!HasAlert)
        {
            return $"{start} Pas d'alerte : le solde ne passe pas sous zéro "
                + $"(coussin de trésorerie nécessaire : {Euros(CashBuffer)}).";
        }

        // No buffer needed although the balance is below zero: the month starts overdrawn, nothing digs deeper.
        var cause = CashBuffer > 0m
            ? $"Il faudrait démarrer le mois avec au moins {Euros(CashBuffer)} pour l'éviter."
            : "Le compte démarre déjà à découvert : les mouvements du mois ne le creusent pas davantage.";
        var debits = ContributingDebits.Count > 0
            ? " Débits qui creusent ce point bas : "
                + string.Join(", ", ContributingDebits.Select(d => $"{d.Name} ({Euros(d.Amount)}, le {Day(d.Date)})")) + "."
            : "";
        return $"{start} Alerte : le compte passe sous zéro. {cause}{debits}";
    }
}

public sealed record DailyBalanceOutput(DateOnly Date, decimal Balance)
{
    public static DailyBalanceOutput From(DailyBalance day) => new(day.Date, Cents(day.Balance));
}

public sealed record DebitOutput(
    [property: Description("Name of the recurring line.")] string Name,
    DateOnly Date,
    decimal Amount)
{
    public static DebitOutput From(Occurrence debit) => new(debit.Line.Name, debit.Date, Cents(debit.Line.Amount));
}
