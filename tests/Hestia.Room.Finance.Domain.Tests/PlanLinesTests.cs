namespace Hestia.Room.Finance.Domain.Tests;

/// <summary>The recurring lines of the plan as a whole: a line created, updated, ended or checked against the others.</summary>
public class PlanLinesTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    private readonly RecurringLine _rent = Line("Loyer");
    // Ended: still a line of the plan, its name still taken.
    private readonly RecurringLine _gym = Line("Salle de sport", end: new DateOnly(2026, 3, 31));

    private PlanLines Plan => new([_rent, _gym]);

    [Fact]
    public void Create_UnderAFreeName_GivesTheLineBack()
    {
        var line = Line("Box internet");

        Assert.Same(line, Plan.Create(line));
    }

    [Theory]
    [InlineData("Loyer")]
    [InlineData("LOYER")]
    // The name of an ended line.
    [InlineData("salle de sport")]
    public void Create_UnderANameAlreadyUsed_Throws_WhateverTheCase(string name)
    {
        var error = Assert.Throws<NameTakenException>(() => Plan.Create(Line(name)));

        Assert.Equal("name", error.ParamName);
    }

    [Fact]
    public void Create_TellsAccentsApart_ButNotHowTheyAreWritten()
    {
        var plan = new PlanLines([Line("Épargne")]);

        Assert.Equal("Epargne", plan.Create(Line("Epargne")).Name);
        // The same name: its "É" written as "E" and a combining accent.
        Assert.Throws<NameTakenException>(() => plan.Create(Line("E" + char.ConvertFromUtf32(0x0301) + "pargne")));
    }

    [Fact]
    public void Create_WithTheIdentifierOfALineOfThePlan_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => Plan.Create(Line("Box internet", id: _rent.Id)));

        Assert.Equal("line", error.ParamName);
    }

    [Fact]
    public void Update_KeepsItsOwnName_OrChangesItsCase()
    {
        Assert.Equal(700m, Plan.Update(Line("Loyer", id: _rent.Id, amount: 700m)).Amount);
        Assert.Equal("LOYER", Plan.Update(Line("LOYER", id: _rent.Id)).Name);
    }

    [Fact]
    public void Update_UnderTheNameOfAnotherLine_Throws()
    {
        Assert.Throws<NameTakenException>(() => Plan.Update(Line("SALLE de sport", id: _rent.Id)));
    }

    [Fact]
    public void Update_OfAnUnknownLine_Throws()
    {
        var error = Assert.Throws<UnknownLineException>(() => Plan.Update(Line("Box internet")));

        Assert.Equal("lineId", error.ParamName);
    }

    [Fact]
    public void End_GivesTheSameLine_EndingOnTheDate()
    {
        var end = new DateOnly(2026, 6, 30);

        var ended = Plan.End(_rent.Id, end);

        Assert.Equal(end, ended.EndDate);
        Assert.Equal(
            (_rent.Id, _rent.Kind, _rent.Name, _rent.Amount, _rent.Frequency, _rent.DayOfMonth, _rent.StartDate, _rent.Category),
            (ended.Id, ended.Kind, ended.Name, ended.Amount, ended.Frequency, ended.DayOfMonth, ended.StartDate, ended.Category));
    }

    [Fact]
    public void End_OfAnEndedLine_MovesItsEnd()
    {
        Assert.Equal(new DateOnly(2026, 5, 31), Plan.End(_gym.Id, new DateOnly(2026, 5, 31)).EndDate);
    }

    [Fact]
    public void End_BeforeTheStart_Throws()
    {
        var error = Assert.Throws<ArgumentException>(() => Plan.End(_rent.Id, Start.AddDays(-1)));

        Assert.Equal("endDate", error.ParamName);
    }

    [Fact]
    public void End_OfAnUnknownLine_Throws()
    {
        Assert.Throws<UnknownLineException>(() => Plan.End(Guid.NewGuid(), new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void EnsureExists_AcceptsALineOfThePlan_EndedOnesIncluded_AndRejectsAnyOther()
    {
        Plan.EnsureExists(_rent.Id);
        Plan.EnsureExists(_gym.Id);

        Assert.Throws<UnknownLineException>(() => Plan.EnsureExists(Guid.NewGuid()));
    }

    private static RecurringLine Line(string name, Guid? id = null, decimal amount = 650m, DateOnly? end = null) =>
        new(id ?? Guid.NewGuid(), FlowKind.Expense, name, amount, Frequency.Monthly, 5, Start, endDate: end, category: Category.Housing);
}
