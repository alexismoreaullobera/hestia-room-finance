namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The recurring lines of the plan as a whole: the rules that involve several lines, over every month (ended lines
/// included). A line comes built by the caller, so it has passed the rules of its own constructor; each method gives the
/// line to save, or throws: the caller writes nothing on a refusal.
/// </summary>
public sealed class PlanLines(IEnumerable<RecurringLine> lines)
{
    private readonly IReadOnlyList<RecurringLine> _lines = lines.ToList().AsReadOnly();

    /// <summary>A new line, under a name no other line uses, ended ones included.</summary>
    public RecurringLine Create(RecurringLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (_lines.Any(other => other.Id == line.Id))
        {
            throw new ArgumentException("A new line has an identifier of its own.", nameof(line));
        }

        EnsureNameFree(line.Name, except: null);
        return line;
    }

    /// <summary>An existing line changed, everywhere (past months included: no versioning).</summary>
    public RecurringLine Update(RecurringLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureExists(line.Id);
        // Its own name is free for itself: a change of amount, or of case only, is no conflict.
        EnsureNameFree(line.Name, except: line.Id);
        return line;
    }

    /// <summary>
    /// An existing line ended on a date: it runs until then, then leaves the months after. Never before its start date
    /// (the rule of the line). An ended line can be ended again on another date.
    /// </summary>
    public RecurringLine End(Guid lineId, DateOnly endDate) => Find(lineId).EndingOn(endDate);

    /// <summary>Rejects a line that is not in the plan: before an update, an end or a delete, never silently ignored.</summary>
    public void EnsureExists(Guid lineId) => Find(lineId);

    private RecurringLine Find(Guid lineId) =>
        _lines.FirstOrDefault(line => line.Id == lineId) ?? throw new UnknownLineException(nameof(lineId));

    private void EnsureNameFree(string name, Guid? except)
    {
        if (_lines.Any(line => line.Id != except && Guard.IsSameName(line.Name, name)))
        {
            throw new NameTakenException(nameof(name));
        }
    }
}
