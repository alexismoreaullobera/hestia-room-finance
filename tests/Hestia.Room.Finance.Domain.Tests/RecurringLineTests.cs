namespace Hestia.Room.Finance.Domain.Tests;

public class RecurringLineTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [Fact]
    public void Constructor_WithAValidMonthlyExpense_KeepsEveryValue()
    {
        var id = Guid.NewGuid();

        var line = new RecurringLine(id, FlowKind.Expense, "Rent", 650m, Frequency.Monthly, dayOfMonth: 5, Start, category: "Housing");

        Assert.Equal(id, line.Id);
        Assert.Equal(FlowKind.Expense, line.Kind);
        Assert.Equal("Rent", line.Name);
        Assert.Equal(650m, line.Amount);
        Assert.Equal(Frequency.Monthly, line.Frequency);
        Assert.Equal(5, line.DayOfMonth);
        Assert.Equal(Start, line.StartDate);
        Assert.Null(line.AnchorMonth);
        Assert.Null(line.EndDate);
        Assert.Equal("Housing", line.Category);
        Assert.False(line.IsSubscription);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithoutName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => Monthly(name: name));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void Constructor_WithAmountNotPositive_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Monthly(amount: amount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public void Constructor_WithDayOutOfRange_Throws(int day)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Monthly(day: day));
    }

    [Fact]
    public void Constructor_MonthlyWithAnAnchorMonth_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Rent", 650m, Frequency.Monthly, 5, Start, anchorMonth: 3));

        Assert.Equal("anchorMonth", error.ParamName);
    }

    [Theory]
    [InlineData(Frequency.Quarterly, null)]
    [InlineData(Frequency.Yearly, null)]
    [InlineData(Frequency.Yearly, 0)]
    [InlineData(Frequency.Yearly, 13)]
    public void Constructor_QuarterlyOrYearlyWithoutAValidAnchorMonth_Throws(Frequency frequency, int? anchorMonth)
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Car insurance", 360m, frequency, 10, Start, anchorMonth: anchorMonth));

        Assert.Equal("anchorMonth", error.ParamName);
    }

    [Fact]
    public void Constructor_YearlyWithAnAnchorMonth_IsValid()
    {
        var line = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Car insurance", 360m, Frequency.Yearly, 10, Start, anchorMonth: 3);

        Assert.Equal(3, line.AnchorMonth);
    }

    [Fact]
    public void Constructor_WithEndDateBeforeStartDate_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Gym", 30m, Frequency.Monthly, 1, Start, endDate: Start.AddDays(-1)));

        Assert.Equal("endDate", error.ParamName);
    }

    [Fact]
    public void Constructor_AnExpenseWithSubscriptionInfo_IsASubscription()
    {
        var commitmentEnd = new DateOnly(2026, 12, 31);

        var line = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Mobile plan", 15.99m, Frequency.Monthly, 8, Start,
            subscription: new SubscriptionInfo(commitmentEnd));

        Assert.True(line.IsSubscription);
        Assert.Equal(commitmentEnd, line.Subscription!.CommitmentEndDate);
    }

    [Fact]
    public void Constructor_AnIncomeWithSubscriptionInfo_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Income, "Salary", 2000m, Frequency.Monthly, 28, Start,
                subscription: new SubscriptionInfo()));

        Assert.Equal("subscription", error.ParamName);
    }

    private static RecurringLine Monthly(string name = "Rent", decimal amount = 650m, int day = 5) =>
        new(Guid.NewGuid(), FlowKind.Expense, name, amount, Frequency.Monthly, day, Start);
}
