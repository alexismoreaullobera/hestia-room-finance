using System.Globalization;
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

    // decimal is not allowed in attributes (InlineData): the cases are written in a TheoryData instead.
    public static TheoryData<decimal> BoundaryBalances => [999_999_999.99m, -999_999_999.99m];

    public static TheoryData<decimal> BalancesTooLarge => [1_000_000_000m, -1_000_000_000m, decimal.MaxValue, decimal.MinValue];

    [Theory]
    [MemberData(nameof(BoundaryBalances))]
    public void StartingBalance_UpToTheBound_IsValid(decimal balance)
    {
        Assert.Equal(balance, new MonthPlan(April, balance).StartingBalance);
    }

    [Theory]
    [MemberData(nameof(BalancesTooLarge))]
    public void StartingBalance_BeyondTheBound_Throws(decimal balance)
    {
        // Written as literals, not with AmountLimit.Max: the bound is a rule of the room, changing it must break a test.
        var error = Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(April, balance));

        Assert.Equal("startingBalance", error.ParamName);
    }

    [Fact]
    public void StartingBalance_BeyondTheBound_StatesItInvariantly()
    {
        // The message reaches the logs (a row read back goes through the domain): the same in every culture.
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            Assert.Contains("999999999.99", Assert.Throws<ArgumentOutOfRangeException>(() => new MonthPlan(April, 1_000_000_000m)).Message);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Equality_IsByValue()
    {
        Assert.Equal(new MonthPlan(April, 30m), new MonthPlan(April, 30m));
    }
}
