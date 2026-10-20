namespace Hestia.Room.Finance.Domain.Tests;

public class OccurrenceTests
{
    private static readonly DateOnly LongAgo = new(2020, 1, 1);

    [Fact]
    public void OccurrenceIn_MonthlyLine_FallsOnItsDay()
    {
        var rent = Line(Frequency.Monthly, day: 5);

        var occurrence = rent.OccurrenceIn(new YearMonth(2026, 10));

        Assert.NotNull(occurrence);
        Assert.Same(rent, occurrence.Line);
        Assert.Equal(new DateOnly(2026, 10, 5), occurrence.Date);
    }

    [Theory]
    [InlineData(31, 2026, 4, 30)]
    [InlineData(31, 2026, 2, 28)]
    [InlineData(30, 2026, 2, 28)]
    [InlineData(31, 2028, 2, 29)] // leap year
    [InlineData(29, 2028, 2, 29)]
    [InlineData(31, 2026, 1, 31)]
    public void OccurrenceIn_DayBeyondTheEndOfTheMonth_FallsOnItsLastDay(int day, int year, int month, int expectedDay)
    {
        var occurrence = Line(Frequency.Monthly, day).OccurrenceIn(new YearMonth(year, month));

        Assert.Equal(new DateOnly(year, month, expectedDay), occurrence!.Date);
    }

    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(1, 4, true)]
    [InlineData(1, 7, true)]
    [InlineData(1, 10, true)]
    [InlineData(1, 2, false)]
    [InlineData(1, 12, false)]
    [InlineData(11, 11, true)] // anchor late in the year: November, February, May, August
    [InlineData(11, 2, true)]
    [InlineData(11, 5, true)]
    [InlineData(11, 8, true)]
    [InlineData(11, 1, false)]
    public void OccurrenceIn_QuarterlyLine_IsDueEveryThreeMonthsFromItsAnchor(int anchorMonth, int month, bool isDue)
    {
        var water = Line(Frequency.Quarterly, day: 15, anchorMonth: anchorMonth);

        Assert.Equal(isDue, water.OccurrenceIn(new YearMonth(2026, month)) is not null);
    }

    [Theory]
    [InlineData(3, true)]
    [InlineData(2, false)]
    [InlineData(4, false)]
    public void OccurrenceIn_YearlyLine_IsDueOnlyInItsAnchorMonth(int month, bool isDue)
    {
        var insurance = Line(Frequency.Yearly, day: 10, anchorMonth: 3);

        Assert.Equal(isDue, insurance.OccurrenceIn(new YearMonth(2026, month)) is not null);
    }

    [Fact]
    public void OccurrenceIn_DueDateBeforeTheStartDate_IsNull()
    {
        // Starts on the 15th, due on the 5th: nothing in January, first occurrence in February.
        var gym = Line(Frequency.Monthly, day: 5, start: new DateOnly(2026, 1, 15));

        Assert.Null(gym.OccurrenceIn(new YearMonth(2026, 1)));
        Assert.Equal(new DateOnly(2026, 2, 5), gym.OccurrenceIn(new YearMonth(2026, 2))!.Date);
    }

    [Fact]
    public void OccurrenceIn_DueOnTheStartDate_IsIncluded()
    {
        var gym = Line(Frequency.Monthly, day: 15, start: new DateOnly(2026, 1, 15));

        Assert.Equal(new DateOnly(2026, 1, 15), gym.OccurrenceIn(new YearMonth(2026, 1))!.Date);
    }

    [Fact]
    public void OccurrenceIn_MonthBeforeTheStartDate_IsNull()
    {
        var gym = Line(Frequency.Monthly, day: 5, start: new DateOnly(2026, 6, 1));

        Assert.Null(gym.OccurrenceIn(new YearMonth(2026, 5)));
    }

    [Fact]
    public void OccurrenceIn_DueOnTheEndDate_IsIncluded()
    {
        var gym = Line(Frequency.Monthly, day: 5, end: new DateOnly(2026, 3, 5));

        Assert.Equal(new DateOnly(2026, 3, 5), gym.OccurrenceIn(new YearMonth(2026, 3))!.Date);
    }

    [Fact]
    public void OccurrenceIn_DueDateAfterTheEndDate_IsNull()
    {
        // Ends on the 4th, due on the 5th: the March debit no longer happens.
        var gym = Line(Frequency.Monthly, day: 5, end: new DateOnly(2026, 3, 4));

        Assert.Null(gym.OccurrenceIn(new YearMonth(2026, 3)));
        Assert.Null(gym.OccurrenceIn(new YearMonth(2026, 4)));
    }

    private static RecurringLine Line(Frequency frequency, int day, int? anchorMonth = null, DateOnly? start = null, DateOnly? end = null) =>
        new(Guid.NewGuid(), FlowKind.Expense, "Line", 10m, frequency, day, start ?? LongAgo, anchorMonth, end);
}
