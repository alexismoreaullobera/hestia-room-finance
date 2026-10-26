namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The recurring lines of the plan as a whole: the rules that involve several lines, over every month (ended and future
/// lines included). A line comes built by the caller, so it has passed the rules of its own constructor; each method
/// gives the line to save, or throws: the caller writes nothing on a refusal.
/// </summary>
public sealed class PlanLines(IEnumerable<RecurringLine> lines)
{
    private readonly IReadOnlyList<RecurringLine> _lines = lines.ToList().AsReadOnly();

    /// <summary>A new line, under a name no other line uses at the same time.</summary>
    public RecurringLine Create(RecurringLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_lines.Any(other => other.Id == line.Id))
        {
            throw new ArgumentException("A new line has an identifier of its own.", nameof(line));
        }

        EnsureNameFree(line, except: null);
        return line;
    }

    /// <summary>An existing line changed, everywhere (past months included: no versioning).</summary>
    public RecurringLine Update(RecurringLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureExists(line.Id);
        // Its own name is free for itself: a change of amount, or of case only, is no conflict.
        EnsureNameFree(line, except: line.Id);
        return line;
    }

    /// <summary>
    /// An existing line ended on a date: it runs until then, then leaves the months after. Never before its start date
    /// (the rule of the line). An ended line can be ended again on another date, never so late that it meets a line
    /// of the same name.
    /// </summary>
    public RecurringLine End(Guid lineId, DateOnly endDate)
    {
        var ended = Find(lineId).EndingOn(endDate);
        EnsureNameFree(ended, except: lineId);
        return ended;
    }

    /// <summary>Rejects a line that is not in the plan: before an update, an end or a delete, never silently ignored.</summary>
    public void EnsureExists(Guid lineId) => Find(lineId);

    private RecurringLine Find(Guid lineId) =>
        _lines.FirstOrDefault(line => line.Id == lineId) ?? throw new UnknownLineException(nameof(lineId));

    /// <summary>
    /// Two lines share a name only one after the other: a rent ended in December, another one from January (the way to
    /// change a line from a month on). Never at the same time, even for a day: the two would read as one line.
    /// </summary>
    private void EnsureNameFree(RecurringLine line, Guid? except)
    {
        if (_lines.Any(other => other.Id != except && Guard.IsSameName(other.Name, line.Name) && RunTogether(other, line)))
        {
            // The argument at fault is the name the user typed.
            throw new NameTakenException("name");
        }
    }

    /// <summary>The two periods share at least one day (a line without end runs forever).</summary>
    private static bool RunTogether(RecurringLine first, RecurringLine second) =>
        first.StartDate <= (second.EndDate ?? DateOnly.MaxValue) && second.StartDate <= (first.EndDate ?? DateOnly.MaxValue);
}
