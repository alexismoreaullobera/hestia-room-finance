using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Time;
using Hestia.Room.Finance.Mcp.Tools;
using Microsoft.Extensions.Time.Testing;
using ModelContextProtocol;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class MonthArgumentTests
{
    private static readonly FinanceClock InOctober2026 = new(new FakeTimeProvider(new DateTimeOffset(2026, 10, 15, 12, 0, 0, TimeSpan.Zero)));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_WithoutMonth_GivesTheCurrentMonth(string? month)
    {
        Assert.Equal(new YearMonth(2026, 10), MonthArgument.Resolve(month, InOctober2026));
    }

    [Fact]
    public void Resolve_WithAMonth_GivesThatMonth()
    {
        Assert.Equal(new YearMonth(2026, 4), MonthArgument.Resolve("2026-04", InOctober2026));
    }

    [Theory]
    [InlineData("2026-13")]
    [InlineData("avril")]
    public void Resolve_WithAnInvalidMonth_ThrowsAToolErrorTheAgentCanFix(string month)
    {
        // McpException: its message reaches the agent in the tool result (isError), so it can call again.
        var error = Assert.Throws<McpException>(() => MonthArgument.Resolve(month, InOctober2026));

        Assert.Contains(month, error.Message);
        Assert.Contains("YYYY-MM", error.Message);
    }

    [Fact]
    public void Parse_WithAMonth_GivesThatMonth()
    {
        Assert.Equal(new YearMonth(2026, 4), MonthArgument.Parse("2026-04"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("2026-13")]
    public void Parse_WithoutAValidMonth_ThrowsInsteadOfTakingTheCurrentMonth(string? month)
    {
        // An input tool never writes to a month by default: the view always says which month it changes.
        var error = Assert.Throws<McpException>(() => MonthArgument.Parse(month));

        Assert.Contains("YYYY-MM", error.Message);
    }

    [Fact]
    public void Resolve_WithALongInvalidMonth_DoesNotEchoIt()
    {
        // The message goes back into the model context: a long value is never copied there.
        var month = new string('x', 200);

        var error = Assert.Throws<McpException>(() => MonthArgument.Resolve(month, InOctober2026));

        Assert.DoesNotContain(month, error.Message);
        Assert.Contains("too long", error.Message);
    }
}
