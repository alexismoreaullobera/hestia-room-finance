namespace Hestia.Room.Finance.Domain.Tests;

public class YearMonthTests
{
    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(0, 1)]
    [InlineData(10000, 1)]
    public void Constructor_WithYearOrMonthOutOfRange_Throws(int year, int month)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new YearMonth(year, month));
    }

    [Theory]
    [InlineData(2026, 1, 31)]
    [InlineData(2026, 4, 30)]
    [InlineData(2026, 2, 28)]
    [InlineData(2028, 2, 29)] // leap year
    public void DaysInMonth_FollowsTheCalendar(int year, int month, int expectedDays)
    {
        Assert.Equal(expectedDays, new YearMonth(year, month).DaysInMonth);
    }

    [Fact]
    public void FirstDayAndLastDay_BoundTheMonth()
    {
        var april = new YearMonth(2026, 4);

        Assert.Equal(new DateOnly(2026, 4, 1), april.FirstDay);
        Assert.Equal(new DateOnly(2026, 4, 30), april.LastDay);
    }

    [Fact]
    public void From_KeepsTheYearAndMonthOfADate()
    {
        Assert.Equal(new YearMonth(2026, 10), YearMonth.From(new DateOnly(2026, 10, 20)));
    }

    [Theory]
    [InlineData(2026, 4, 1, true)]
    [InlineData(2026, 4, 30, true)]
    [InlineData(2026, 3, 31, false)]
    [InlineData(2026, 5, 1, false)]
    [InlineData(2025, 4, 15, false)]
    public void Contains_OnlyTheDatesOfThatMonth(int year, int month, int day, bool expected)
    {
        Assert.Equal(expected, new YearMonth(2026, 4).Contains(new DateOnly(year, month, day)));
    }

    [Theory]
    [InlineData(2026, 4, "2026-04")]
    [InlineData(2026, 12, "2026-12")]
    public void ToString_IsYearDashMonth(int year, int month, string expected)
    {
        Assert.Equal(expected, new YearMonth(year, month).ToString());
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new YearMonth(2026, 4), new YearMonth(2026, 4));
        Assert.NotEqual(new YearMonth(2026, 4), new YearMonth(2026, 5));
    }
}
