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

    private static RecurringLine Line(Frequency frequency, decimal amount, int? anchorMonth = null) =>
        new(Guid.NewGuid(), FlowKind.Expense, "Line", amount, frequency, 10, new DateOnly(2026, 1, 1), anchorMonth);
}
