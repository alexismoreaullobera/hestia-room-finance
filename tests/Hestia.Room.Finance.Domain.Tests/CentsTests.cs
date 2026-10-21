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

    [Theory]
    [InlineData(650)]
    [InlineData(15.9)]
    [InlineData(13.49)]
    public void Amounts_WithAtMostTwoDecimals_AreValid(double amount)
    {
        // double here only to write the cases: converted to decimal exactly (one or two decimals).
        var line = new RecurringLine(Guid.NewGuid(), FlowKind.Expense, "Line", (decimal)amount, Frequency.Monthly, 5, LongAgo);

        Assert.Equal((decimal)amount, line.Amount);
    }
}
