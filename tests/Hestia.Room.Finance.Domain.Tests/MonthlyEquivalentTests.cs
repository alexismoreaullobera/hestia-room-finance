namespace Hestia.Room.Finance.Domain.Tests;

public class MonthlyEquivalentTests
{
    [Fact]
    public void MonthlyEquivalent_OfAMonthlyLine_IsItsAmount()
    {
        Assert.Equal(13.49m, Line(Frequency.Monthly, 13.49m).MonthlyEquivalent);
    }

    [Fact]
    public void MonthlyEquivalent_OfAQuarterlyLine_IsAThirdOfItsAmount()
    {
        Assert.Equal(30m, Line(Frequency.Quarterly, 90m, anchorMonth: 1).MonthlyEquivalent);
    }

    [Fact]
    public void MonthlyEquivalent_OfAYearlyLine_IsATwelfthOfItsAmount()
    {
        Assert.Equal(30m, Line(Frequency.Yearly, 360m, anchorMonth: 3).MonthlyEquivalent);
    }

    [Fact]
    public void MonthlyEquivalent_IsNotRoundedToTheCent()
    {
        // 100 € a year: 8.333... per month, rounded only when displayed.
        var equivalent = Line(Frequency.Yearly, 100m, anchorMonth: 3).MonthlyEquivalent;

        Assert.Equal(100m / 12m, equivalent);
        Assert.Equal(8.33m, Math.Round(equivalent, 2));
    }

    // decimal is not allowed in attributes (InlineData): the cases are written in a TheoryData instead.
    public static TheoryData<Frequency, decimal, int?, decimal> YearlyCases => new()
    {
        { Frequency.Monthly, 13.49m, null, 161.88m },
        { Frequency.Quarterly, 100m, 1, 400m },
        { Frequency.Yearly, 180m, 3, 180m },
    };

    [Theory]
    [MemberData(nameof(YearlyCases))]
    public void YearlyAmount_CountsTheDebitsOfAYear(Frequency frequency, decimal amount, int? anchorMonth, decimal expected)
    {
        Assert.Equal(expected, Line(frequency, amount, anchorMonth).YearlyAmount);
    }

    private static RecurringLine Line(Frequency frequency, decimal amount, int? anchorMonth = null) =>
        new(Guid.NewGuid(), FlowKind.Expense, "Line", amount, frequency, 10, new DateOnly(2026, 1, 1), anchorMonth);
}
