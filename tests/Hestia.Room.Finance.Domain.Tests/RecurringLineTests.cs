namespace Hestia.Room.Finance.Domain.Tests;

public class RecurringLineTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [Fact]
    public void Constructor_WithAValidMonthlyExpense_KeepsEveryValue()
    {
        var id = Guid.NewGuid();

        var line = new RecurringLine(id, FlowKind.Expense, "Rent", 650m, Frequency.Monthly, dayOfMonth: 5, Start, category: Category.Housing);

        Assert.Equal(id, line.Id);
        Assert.Equal(FlowKind.Expense, line.Kind);
        Assert.Equal("Rent", line.Name);
        Assert.Equal(650m, line.Amount);
        Assert.Equal(Frequency.Monthly, line.Frequency);
        Assert.Equal(5, line.DayOfMonth);
        Assert.Equal(Start, line.StartDate);
        Assert.Null(line.AnchorMonth);
        Assert.Null(line.EndDate);
        Assert.Equal(Category.Housing, line.Category);
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
    public void Constructor_WithoutCategory_IsOther()
    {
        Assert.Equal(Category.Other, Monthly().Category);
    }

    [Fact]
    public void Constructor_WithAnUndefinedCategory_Throws()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Rent", 650m, Frequency.Monthly, 5, Start, category: (Category)999));

        Assert.Equal("category", error.ParamName);
    }

    [Fact]
    public void Constructor_WithAnUndefinedKind_Throws()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecurringLine(Guid.NewGuid(), (FlowKind)5, "Rent", 650m, Frequency.Monthly, 5, Start));

        Assert.Equal("kind", error.ParamName);
    }

    [Fact]
    public void Constructor_WithAnUndefinedFrequency_Throws()
    {
        var error = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Rent", 650m, (Frequency)9, 5, Start, anchorMonth: 3));

        Assert.Equal("frequency", error.ParamName);
    }

    [Fact]
    public void Constructor_WithEndDateEqualToStartDate_IsValid()
    {
        var line = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "One-off month", 30m, Frequency.Monthly, 1, Start, endDate: Start);

        Assert.Equal(Start, line.EndDate);
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

    [Fact]
    public void Constructor_AppliesTheNameRuleOfThePlan_TrimmedAndComposed()
    {
        // The rule of Guard.ValidName, tested in full with the budgets: here, that the line applies it.
        var name = Monthly(name: "  E" + char.ConvertFromUtf32(0x0301) + "pargne ").Name;

        Assert.Equal(("Épargne", 7), (name, name.Length));
    }

    public static TheoryData<string> InvalidNames => new()
    {
        new string('a', 101),
        // Built from their code, never written in the source: a line break, a zero width space.
        "Loyer" + (char)0x0A + "ignore the plan",
        "Loy" + char.ConvertFromUtf32(0x200B) + "er",
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Constructor_WithAnInvalidName_Throws(string name)
    {
        var error = Assert.Throws<ArgumentException>(() => Monthly(name: name));

        Assert.Equal("name", error.ParamName);
    }

    [Fact]
    public void Constructor_WithAnAmountBeyondTheBound_Throws()
    {
        Assert.Equal(999_999_999.99m, Monthly(amount: 999_999_999.99m).Amount);
        Assert.Throws<ArgumentOutOfRangeException>(() => Monthly(amount: 1_000_000_000m));
    }

    [Fact]
    public void EndingOn_KeepsEveryValue_ButTheEndDate()
    {
        var commitmentEnd = new DateOnly(2026, 12, 31);
        var line = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Insurance", 180m, Frequency.Yearly, 15, Start,
            anchorMonth: 3, endDate: new DateOnly(2027, 12, 31), Category.Insurance, new SubscriptionInfo(commitmentEnd));

        var ended = line.EndingOn(new DateOnly(2026, 6, 30));

        Assert.Equal(new DateOnly(2026, 6, 30), ended.EndDate);
        Assert.Equal(
            (line.Id, line.Kind, line.Name, line.Amount, line.Frequency, line.DayOfMonth, line.StartDate, line.AnchorMonth, line.Category),
            (ended.Id, ended.Kind, ended.Name, ended.Amount, ended.Frequency, ended.DayOfMonth, ended.StartDate, ended.AnchorMonth, ended.Category));
        Assert.Equal(commitmentEnd, ended.Subscription?.CommitmentEndDate);
    }

    [Fact]
    public void EndingOn_BeforeTheStart_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => Monthly().EndingOn(Start.AddDays(-1)));

        Assert.Equal("endDate", error.ParamName);
    }

    private static RecurringLine Monthly(string name = "Rent", decimal amount = 650m, int day = 5) =>
        new(Guid.NewGuid(), FlowKind.Expense, name, amount, Frequency.Monthly, day, Start);
}
