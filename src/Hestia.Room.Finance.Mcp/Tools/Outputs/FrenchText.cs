using System.Globalization;
using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Formats of the text summaries, written in French for the user.</summary>
internal static class FrenchText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    /// <summary>The domain keeps exact amounts (a 100 € quarterly line weighs 33.333… € a month); outputs show cents.</summary>
    public static decimal Cents(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static string Euros(decimal amount) => Cents(amount).ToString("C", French);

    /// <summary>"octobre 2026" for "2026-10", the month format of the outputs.</summary>
    public static string MonthName(string month) =>
        YearMonth.TryParse(month, out var parsed) ? parsed.FirstDay.ToString("MMMM yyyy", French) : month;

    public static string Day(DateOnly date) => date.ToString("dd/MM", French);

    public static string FullDate(DateOnly date) => date.ToString("dd/MM/yyyy", French);

    public static string FrequencyName(Frequency frequency) => frequency switch
    {
        Frequency.Monthly => "mensuel",
        Frequency.Quarterly => "trimestriel",
        Frequency.Yearly => "annuel",
        _ => frequency.ToString(),
    };
}
