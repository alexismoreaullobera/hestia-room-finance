using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Time;
using Microsoft.Extensions.Time.Testing;

namespace Hestia.Room.Finance.Mcp.Tests.Time;

public class FinanceClockTests
{
    [Theory]
    // Winter (UTC+1): 31 October 2026 at 23:30 in Paris, still October.
    [InlineData("2026-10-31T22:30:00Z", 2026, 10)]
    // Winter: 1 November at 00:30 in Paris, while it is still 31 October in UTC.
    [InlineData("2026-10-31T23:30:00Z", 2026, 11)]
    // Summer (UTC+2): 1 July at 00:30 in Paris, still 30 June in UTC.
    [InlineData("2026-06-30T22:30:00Z", 2026, 7)]
    // New year in Paris, still the previous year in UTC.
    [InlineData("2026-12-31T23:30:00Z", 2027, 1)]
    // 31 October 2027: summer time ends that very night. 23:30 in Paris (UTC+1 again), still October...
    [InlineData("2027-10-31T22:30:00Z", 2027, 10)]
    // ...and midnight in Paris: November.
    [InlineData("2027-10-31T23:00:00Z", 2027, 11)]
    // The exact second of the change, in summer: 23:59:59 then 00:00:00 in Paris.
    [InlineData("2026-06-30T21:59:59Z", 2026, 6)]
    [InlineData("2026-06-30T22:00:00Z", 2026, 7)]
    public void CurrentMonth_FollowsTheCalendarOfParis(string utcNow, int expectedYear, int expectedMonth)
    {
        var clock = new FinanceClock(new FakeTimeProvider(DateTimeOffset.Parse(utcNow, CultureInfo.InvariantCulture)));

        Assert.Equal(new YearMonth(expectedYear, expectedMonth), clock.CurrentMonth());
    }
}
