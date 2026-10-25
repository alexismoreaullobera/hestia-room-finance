using System.Text.Json.Serialization;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// What happened to a recurring line due this month: the status argument of set_line_actual and a field of the month
/// summary. Always written as text. On input, the SDK also tolerates a number or another casing; a number outside the
/// enum is refused by the tool. The converter keeps the text form without the SDK options (native AOT).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LineActualStatus>))]
public enum LineActualStatus
{
    /// <summary>Paid or received as planned: nothing entered.</summary>
    AsPlanned,

    /// <summary>Happened with another amount.</summary>
    Corrected,

    /// <summary>Did not happen this month.</summary>
    Skipped,
}
