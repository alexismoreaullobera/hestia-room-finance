namespace Hestia.Room.Finance.Domain.Tests;

/// <summary>The budgets of the plan as a whole: a budget created, updated or checked against the others.</summary>
public class PlanBudgetsTests
{
    private readonly Budget _groceries = new(Guid.NewGuid(), "Courses", 320m);
    private readonly Budget _outings = new(Guid.NewGuid(), "Sorties", 120m);

    private PlanBudgets Plan => new([_groceries, _outings]);

    [Fact]
    public void Create_UnderAFreeName_IsANewBudget_WithItsNameTrimmed()
    {
        var id = Guid.NewGuid();

        var budget = Plan.Create(id, "  Carburant ", 90m);

        Assert.Equal((id, "Carburant", 90m), (budget.Id, budget.Name, budget.PlannedAmount));
    }

    [Theory]
    [InlineData("Courses")]
    [InlineData("courses")]
    [InlineData(" COURSES ")]
    public void Create_UnderANameAlreadyUsed_Throws_WhateverTheCase(string name)
    {
        var error = Assert.Throws<NameTakenException>(() => Plan.Create(Guid.NewGuid(), name, 90m));

        Assert.Equal("name", error.ParamName);
    }

    [Fact]
    public void Update_KeepsItsOwnName_OrChangesItsCase()
    {
        // Its own name is no conflict: a change of amount, or of case only.
        Assert.Equal(350m, Plan.Update(_groceries.Id, "Courses", 350m).PlannedAmount);
        Assert.Equal("COURSES", Plan.Update(_groceries.Id, "COURSES", 320m).Name);
    }

    [Fact]
    public void Update_UnderTheNameOfAnotherBudget_Throws()
    {
        Assert.Throws<NameTakenException>(() => Plan.Update(_groceries.Id, "sorties", 320m));
    }

    [Fact]
    public void Update_OfAnUnknownBudget_Throws()
    {
        var error = Assert.Throws<UnknownBudgetException>(() => Plan.Update(Guid.NewGuid(), "Carburant", 90m));

        Assert.Equal("budgetId", error.ParamName);
    }

    [Fact]
    public void EnsureExists_AcceptsABudgetOfThePlan_AndRejectsAnyOther()
    {
        Plan.EnsureExists(_outings.Id);

        Assert.Throws<UnknownBudgetException>(() => Plan.EnsureExists(Guid.NewGuid()));
    }

    [Fact]
    public void CreateAndUpdate_KeepTheRulesOfABudget()
    {
        // Through the constructor of Budget: a valid name, a positive amount in cents, within the bound.
        Assert.Throws<ArgumentException>(() => Plan.Create(Guid.NewGuid(), " ", 90m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan.Create(Guid.NewGuid(), "Carburant", 0m));
        Assert.Throws<ArgumentOutOfRangeException>(() => Plan.Update(_groceries.Id, "Courses", 1_000_000_000m));
    }
}
