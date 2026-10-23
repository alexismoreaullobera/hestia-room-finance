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
}
