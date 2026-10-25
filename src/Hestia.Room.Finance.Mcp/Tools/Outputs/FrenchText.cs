using System.Globalization;
using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>Formats of the text summaries, written in French for the user.</summary>
internal static class FrenchText
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    public static string Euros(decimal amount) => Amounts.Cents(amount).ToString("C", French);

    /// <summary>
    /// The French order of names, ignoring case: "Épargne" between "Courses" and "Sorties", as a French reader expects
    /// (an ordinal order would put it after "Sorties"). The culture is explicit: never the one of the server.
    /// </summary>
    public static readonly StringComparer NameOrder = StringComparer.Create(French, ignoreCase: true);

    /// <summary>
    /// A name typed by the user, between French quotes, for the text the model reads: the quotes of the name itself are
    /// replaced by single ones, so that a name never closes its own quote and writes outside of it.
    /// </summary>
    public static string Quoted(string name) => $"« {name.Replace('«', '‹').Replace('»', '›')} »";

    /// <summary>"octobre 2026" for "2026-10", the month format of the outputs (written by the outputs themselves).</summary>
    public static string MonthName(string month) =>
        YearMonth.TryParse(month, out var parsed)
            ? parsed.FirstDay.ToString("MMMM yyyy", French)
            : throw new InvalidOperationException($"'{month}' is not an output month (YYYY-MM).");

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
