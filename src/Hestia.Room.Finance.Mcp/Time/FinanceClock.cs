using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Mcp.Time;

/// <summary>
/// The current month of the user, in Paris time: the v1 room has a single user, living in France, and the month
/// changes at midnight in Paris, not in UTC (on 1 November at 00:30 in Paris, it is still 31 October in UTC).
/// The time comes from an injected <see cref="TimeProvider"/>, so that tests can set it.
/// </summary>
public sealed class FinanceClock(TimeProvider time)
{
    // IANA identifier: known by .NET on Linux and on Windows. A Linux container image needs its tzdata package.
    // Resolved by the constructor, not by a static initializer: the server creates the clock at startup, so a missing
    // time zone stops the startup with a clear error instead of failing the first tool call.
    private readonly TimeZoneInfo _paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    public YearMonth CurrentMonth() =>
        YearMonth.From(DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time.GetUtcNow(), _paris).DateTime));
}
