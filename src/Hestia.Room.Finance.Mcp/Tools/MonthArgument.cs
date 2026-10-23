using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Time;
using ModelContextProtocol;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>The optional <c>month</c> argument shared by the data tools.</summary>
internal static class MonthArgument
{
    public const string Description = "Month to read, as YYYY-MM (for instance 2026-04). Defaults to the current month.";

    /// <summary>
    /// The month asked for, or the current month when none is given. An invalid month throws an
    /// <see cref="McpException"/>: the agent receives a tool error with this message and can call again.
    /// </summary>
    public static YearMonth Resolve(string? month, FinanceClock clock)
    {
        if (string.IsNullOrWhiteSpace(month))
        {
            return clock.CurrentMonth();
        }

        return YearMonth.TryParse(month, out var parsed)
            ? parsed
            : throw new McpException($"Invalid month '{month}': expected YYYY-MM, for instance 2026-04.");
    }
}
