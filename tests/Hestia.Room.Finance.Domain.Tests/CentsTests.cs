using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

/// <summary>Amounts in euros have at most two decimals, everywhere in the domain.</summary>
public class CentsTests
{
    private const decimal FractionOfACent = 12.345m;

    [Fact]
    public void RecurringLine_WithAFractionOfACent_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Rent", FractionOfACent, Frequency.Monthly, 5, LongAgo));

        Assert.Equal("amount", error.ParamName);
    }

    [Fact]
    public void Budget_WithAFractionOfACent_Throws()
    {
        Assert.Equal("plannedAmount", Assert.Throws<ArgumentException>(() => NewBudget("Groceries", FractionOfACent)).ParamName);
    }

    [Fact]
    public void BudgetActual_WithAFractionOfACent_Throws()
    {
        Assert.Equal("amount", Assert.Throws<ArgumentException>(() => new BudgetActual(Guid.NewGuid(), April, FractionOfACent)).ParamName);
    }

    [Fact]
    public void RecurringOverride_WithAFractionOfACent_Throws()
    {
        Assert.Equal("actualAmount", Assert.Throws<ArgumentException>(() =>
            RecurringOverride.WithAmount(Guid.NewGuid(), April, FractionOfACent)).ParamName);
    }

    [Fact]
    public void MonthPlan_WithAFractionOfACent_Throws()
    {
        Assert.Equal("startingBalance", Assert.Throws<ArgumentException>(() => new MonthPlan(April, FractionOfACent)).ParamName);
    }

    // decimal is not allowed in attributes (InlineData): the cases are written in a TheoryData instead.
    public static TheoryData<decimal> AmountsInCents => [650m, 15.9m, 13.49m, 12.340m];

    [Theory]
    [MemberData(nameof(AmountsInCents))] // 12.340m: three digits after the point, but a whole number of cents
    public void Amounts_WithAtMostTwoDecimals_AreValid(decimal amount)
    {
        var line = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Line", amount, Frequency.Monthly, 5, LongAgo);

        Assert.Equal(amount, line.Amount);
    }
}
