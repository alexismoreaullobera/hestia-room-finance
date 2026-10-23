namespace Hestia.Room.Finance.Domain;

public static class OccurrenceExtensions
{
    /// <summary>
    /// The occurrences of the given lines in a month, ordered by date, then by name on the same day: the order never
    /// depends on the order of the lines (a database read without ORDER BY gives them in any order).
    /// </summary>
    public static IReadOnlyList<Occurrence> OccurrencesIn(this IEnumerable<RecurringLine> lines, YearMonth month) =>
        lines
            .Select(line => line.OccurrenceIn(month))
            .OfType<Occurrence>()
            .OrderBy(occurrence => occurrence.Date)
            .ThenBy(occurrence => occurrence.Line.Name, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();
}
