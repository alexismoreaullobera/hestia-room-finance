using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class EmptyIdentifierTests
{
    [Fact]
    public void RecurringLine_WithAnEmptyId_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() =>
            new RecurringLine(Guid.Empty, FlowKind.Expense, "Rent", 650m, Frequency.Monthly, 5, LongAgo));

        Assert.Equal("id", error.ParamName);
    }

    [Fact]
    public void Budget_WithAnEmptyId_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => new Budget(Guid.Empty, "Groceries", 200m));

        Assert.Equal("id", error.ParamName);
    }

    [Fact]
    public void BudgetActual_WithAnEmptyBudgetId_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => new BudgetActual(Guid.Empty, April, 100m));

        Assert.Equal("budgetId", error.ParamName);
    }

    [Fact]
    public void RecurringOverride_WithAnEmptyLineId_Throws()
    {
        Assert.Equal("lineId", Assert.Throws<ArgumentException>(() => RecurringOverride.WithAmount(Guid.Empty, April, 52m)).ParamName);
        Assert.Equal("lineId", Assert.Throws<ArgumentException>(() => RecurringOverride.Skipped(Guid.Empty, April)).ParamName);
    }
}
