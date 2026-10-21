using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

public class MonthPlanTests
{
    [Fact]
    public void StartingBalance_IsZeroByDefault()
    {
        Assert.Equal(0m, new MonthPlan(April).StartingBalance);
    }

    [Fact]
    public void StartingBalance_CanBeNegative()
    {
        // The account may already be overdrawn at the start of the month.
        Assert.Equal(-100m, new MonthPlan(April, -100m).StartingBalance);
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new MonthPlan(April, 30m), new MonthPlan(April, 30m));
    }
}
