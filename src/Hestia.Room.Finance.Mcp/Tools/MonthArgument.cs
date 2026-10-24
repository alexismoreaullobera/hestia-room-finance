using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Time;
using ModelContextProtocol;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// The <c>month</c> argument of the tools: optional for the data tools and the view tools (a read), required for the
/// input tools (a write never falls back on the current month).
/// </summary>
internal static class MonthArgument
{
    public const string Description = "Month to read, as YYYY-MM (for instance 2026-04). Defaults to the current month.";

    public const string RequiredDescription = "Month to change, as YYYY-MM (for instance 2026-04).";

    private const int MaxEchoedLength = 16;

    /// <summary>
    /// The month asked for, or the current month when none is given. An invalid month throws an
    /// <see cref="McpException"/>: the agent receives a tool error with this message and can call again.
    /// </summary>
    public static YearMonth Resolve(string? month, FinanceClock clock) =>
        string.IsNullOrWhiteSpace(month) ? clock.CurrentMonth() : Parse(month);

    /// <summary>
    /// The month given, which must be one: empty or invalid throws an <see cref="McpException"/>. For the input tools,
    /// where writing to the current month by default would change a month the user did not choose.
    /// </summary>
    public static YearMonth Parse(string? month)
    {
        if (YearMonth.TryParse(month, out var parsed))
        {
            return parsed;
        }

        // The message goes back into the model context: a short value is echoed to help it fix its call, never a long one.
        // Null when a client sends "month": null, whatever the declared type.
        var shown = month switch
        {
            null => "(none)",
            { Length: <= MaxEchoedLength } => $"'{month}'",
            _ => "(too long to show)",
        };
        throw new McpException($"Invalid month {shown}: expected YYYY-MM, for instance 2026-04.");
    }
}
