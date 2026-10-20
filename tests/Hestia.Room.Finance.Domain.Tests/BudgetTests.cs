namespace Hestia.Room.Finance.Domain.Tests;

public class BudgetTests
{
    [Fact]
    public void Constructor_WithValidValues_KeepsThem()
    {
        var id = Guid.NewGuid();

        var budget = new Budget(id, "Groceries", 200m);

        Assert.Equal(id, budget.Id);
        Assert.Equal("Groceries", budget.Name);
        Assert.Equal(200m, budget.PlannedAmount);
    }

    [Fact]
    public void Constructor_WithoutName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Budget(Guid.NewGuid(), " ", 200m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Constructor_WithAmountNotPositive_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Budget(Guid.NewGuid(), "Groceries", amount));
    }
}
