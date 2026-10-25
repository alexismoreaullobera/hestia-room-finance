using static Hestia.Room.Finance.Domain.Tests.TestPlan;

namespace Hestia.Room.Finance.Domain.Tests;

/// <summary>
/// The actuals of a month as the user enters them: only for a budget of the month, or a recurring line due that month
/// (an override of a line not due would be ignored by the calculations, so it is refused when entered).
/// </summary>
public class FinanceMonthInputTests
{
    private readonly RecurringLine _rent = Expense("Rent", 650m, day: 5);
    // Quarterly from April: due in April, as much as a monthly line.
    private readonly RecurringLine _carInsurance = new(Guid.NewGuid(), FlowKind.Expense, "Car insurance", 165m,
        Frequency.Quarterly, dayOfMonth: 15, LongAgo, anchorMonth: 4);
    // Yearly in March: running in April, but not due.
    private readonly RecurringLine _insurance = YearlyExpense("Home insurance", 360m, day: 10, anchorMonth: 3);
    // Ended in March: not running in April.
    private readonly RecurringLine _gym = Expense("Gym", 30m, day: 3, end: new DateOnly(2026, 3, 31));
    private readonly Budget _groceries = NewBudget("Groceries", 200m);

    private FinanceMonth April2026 => new(new MonthPlan(April), [_rent, _carInsurance, _insurance, _gym], [_groceries], [], []);

    [Fact]
    public void Occurrences_AreTheLinesDueThisMonth()
    {
        Assert.Equal([_rent, _carInsurance], April2026.Occurrences.Select(o => o.Line));
    }

    [Fact]
    public void ActualFor_ABudgetOfTheMonth_IsTheActualOfThatMonth()
    {
        Assert.Equal(new BudgetActual(_groceries.Id, April, 235m), April2026.ActualFor(_groceries.Id, 235m));
    }

    [Fact]
    public void ActualFor_AnUnknownBudget_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => April2026.ActualFor(Guid.NewGuid(), 235m));

        Assert.Equal("budgetId", error.ParamName);
    }

    [Fact]
    public void ActualFor_KeepsTheRulesOfAnActual()
    {
        // Through the constructor of BudgetActual: never negative, cents, bound.
        Assert.Throws<ArgumentOutOfRangeException>(() => April2026.ActualFor(_groceries.Id, -1m));
        Assert.Throws<ArgumentException>(() => April2026.ActualFor(_groceries.Id, 12.345m));
        Assert.Throws<ArgumentOutOfRangeException>(() => April2026.ActualFor(_groceries.Id, 1_000_000_000m));
    }

    [Fact]
    public void CorrectionFor_ALineDueThisMonth_IsAnOverrideWithItsAmount()
    {
        Assert.Equal(RecurringOverride.WithAmount(_rent.Id, April, 670m), April2026.CorrectionFor(_rent.Id, 670m));
    }

    [Fact]
    public void SkippedFor_ALineDueThisMonth_IsASkippedOverride()
    {
        Assert.Equal(RecurringOverride.Skipped(_rent.Id, April), April2026.SkippedFor(_rent.Id));
    }

    [Fact]
    public void CorrectionAndSkipped_ForAQuarterlyLineDueThisMonth_AreAccepted()
    {
        Assert.Equal(RecurringOverride.WithAmount(_carInsurance.Id, April, 170m), April2026.CorrectionFor(_carInsurance.Id, 170m));
        Assert.Equal(RecurringOverride.Skipped(_carInsurance.Id, April), April2026.SkippedFor(_carInsurance.Id));
        April2026.EnsureLineDue(_carInsurance.Id);
    }

    public static TheoryData<string> LinesNotDue => ["insurance (yearly, due in March)", "gym (ended in March)", "unknown"];

    [Theory]
    [MemberData(nameof(LinesNotDue))]
    public void CorrectionAndSkipped_ForALineNotDue_Throw(string line)
    {
        var lineId = line switch
        {
            "insurance (yearly, due in March)" => _insurance.Id,
            "gym (ended in March)" => _gym.Id,
            _ => Guid.NewGuid(),
        };

        Assert.Equal("lineId", Assert.Throws<ArgumentException>(() => April2026.CorrectionFor(lineId, 52m)).ParamName);
        Assert.Equal("lineId", Assert.Throws<ArgumentException>(() => April2026.SkippedFor(lineId)).ParamName);
        Assert.Equal("lineId", Assert.Throws<ArgumentException>(() => April2026.EnsureLineDue(lineId)).ParamName);
    }

    [Fact]
    public void Ensure_AcceptsTheBudgetsAndLinesOfTheMonth()
    {
        // Checked before clearing an actual or a correction: no exception for the ones of the month.
        April2026.EnsureBudget(_groceries.Id);
        April2026.EnsureLineDue(_rent.Id);
    }

    [Fact]
    public void EnsureBudget_WithAnUnknownBudget_Throws()
    {
        Assert.Equal("budgetId", Assert.Throws<ArgumentException>(() => April2026.EnsureBudget(Guid.NewGuid())).ParamName);
    }

    public static TheoryData<decimal> AmountsTooLarge => [1_000_000_000m, decimal.MaxValue];

    [Theory]
    [MemberData(nameof(AmountsTooLarge))]
    public void ActualAmounts_BeyondTheBound_Throw(decimal amount)
    {
        // Written as literals: the bound is a rule of the room. Negative amounts are refused by other rules already.
        Assert.Equal("amount", Assert.Throws<ArgumentOutOfRangeException>(() => new BudgetActual(_groceries.Id, April, amount)).ParamName);
        Assert.Equal("actualAmount", Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurringOverride.WithAmount(_rent.Id, April, amount)).ParamName);
    }

    [Fact]
    public void ActualAmounts_AtTheBound_AreValid()
    {
        Assert.Equal(999_999_999.99m, new BudgetActual(_groceries.Id, April, 999_999_999.99m).Amount);
        Assert.Equal(999_999_999.99m, RecurringOverride.WithAmount(_rent.Id, April, 999_999_999.99m).ActualAmount);
    }
}
