namespace Hestia.Room.Finance.Domain;

/// <summary>A recurring line falling due on a given date. Computed, never stored.</summary>
public sealed record Occurrence(RecurringLine Line, DateOnly Date);
