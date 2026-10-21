namespace Hestia.Room.Finance.Domain;

public static class OccurrenceExtensions
{
    /// <summary>The occurrences of the given lines in a month, ordered by date.</summary>
    public static IReadOnlyList<Occurrence> OccurrencesIn(this IEnumerable<RecurringLine> lines, YearMonth month) =>
        lines
            .Select(line => line.OccurrenceIn(month))
            .OfType<Occurrence>()
            .OrderBy(occurrence => occurrence.Date)
            .ToList()
            .AsReadOnly();
}
