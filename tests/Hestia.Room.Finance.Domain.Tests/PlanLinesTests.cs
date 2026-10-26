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
    public void Create_UnderTheNameOfALineEndedBeforeItsStart_IsAccepted()
    {
        // The way to change a line from a month on: the rent ended in December, another one from January.
        var plan = new PlanLines([Line("Loyer", end: new DateOnly(2026, 12, 31))]);

        var next = plan.Create(Line("Loyer", amount: 700m, start: new DateOnly(2027, 1, 1)));

        Assert.Equal(new DateOnly(2027, 1, 1), next.StartDate);
    }

    [Theory]
    // Starting on the last day of the other one: one day together is already the same time.
    [InlineData("2026-12-31", null)]
    // Ending after the other one started.
    [InlineData("2026-06-01", "2027-03-31")]
    // Running before it and ending during it.
    [InlineData("2025-06-01", "2026-02-28")]
    public void Create_UnderTheNameOfALineRunningAtTheSameTime_Throws(string start, string? end)
    {
        var plan = new PlanLines([Line("Loyer", end: new DateOnly(2026, 12, 31))]);

        Assert.Throws<NameTakenException>(() =>
            plan.Create(Line("loyer", start: DateOnly.Parse(start), end: end is null ? null : DateOnly.Parse(end))));
    }

    [Fact]
    public void Update_ReopeningALine_WhenAnotherOfTheSameNameFollows_Throws()
    {
        var ended = Line("Loyer", end: new DateOnly(2026, 12, 31));
        var next = Line("Loyer", start: new DateOnly(2027, 1, 1));
        var plan = new PlanLines([ended, next]);

        Assert.Throws<NameTakenException>(() => plan.Update(Line("Loyer", id: ended.Id)));
    }

    [Fact]
    public void End_SoLateThatItMeetsTheNextLineOfTheSameName_Throws()
    {
        var ended = Line("Loyer", end: new DateOnly(2026, 12, 31));
        var plan = new PlanLines([ended, Line("Loyer", start: new DateOnly(2027, 1, 1))]);

        Assert.Equal(new DateOnly(2026, 11, 30), plan.End(ended.Id, new DateOnly(2026, 11, 30)).EndDate);
        Assert.Throws<NameTakenException>(() => plan.End(ended.Id, new DateOnly(2027, 1, 1)));
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

        // Every other field kept: see RecurringLineTests.EndingOn_KeepsEveryValue_ButTheEndDate.
        Assert.Equal((_rent.Id, end), (ended.Id, ended.EndDate));
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

    private static RecurringLine Line(string name, Guid? id = null, decimal amount = 650m, DateOnly? end = null, DateOnly? start = null) =>
        new(id ?? Guid.NewGuid(), FlowKind.Expense, name, amount, Frequency.Monthly, 5, start ?? Start, endDate: end, category: Category.Housing);
}
