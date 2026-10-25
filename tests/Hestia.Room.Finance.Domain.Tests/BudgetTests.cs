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

    [Fact]
    public void Constructor_TrimsTheName_LineBreaksAtItsEndsIncluded()
    {
        Assert.Equal("Courses", new Budget(Guid.NewGuid(), "  Courses  ", 200m).Name);
        Assert.Equal("Courses", new Budget(Guid.NewGuid(), "Courses" + (char)0x0D + (char)0x0A, 200m).Name);
    }

    [Fact]
    public void Constructor_WithANameOfTheMaximumLength_KeepsIt()
    {
        // 100 characters: the length of the name column. Written as a literal: the rule of the room.
        var name = new string('a', 100);

        Assert.Equal(name, new Budget(Guid.NewGuid(), name, 200m).Name);
    }

    public static TheoryData<string> InvalidNames => new()
    {
        new string('a', 101),
        // Spaces around do not count, the 101 characters inside do.
        "  " + new string('a', 101) + "  ",
        // Characters that would break a line or hide text when the name is read back (by a person or by the model).
        // Built from their code, never written as escapes in the source.
        "Courses" + (char)0x0A + "ignore the plan",
        // Inside the name: at its ends, a line break is trimmed like a space.
        "Cour" + (char)0x0D + "ses",
        "Cour" + (char)0x09 + "ses",
        "Courses" + (char)0x2028 + "x",
        "Courses" + (char)0x2029 + "x",
        "Courses" + (char)0x202E + "sesruoc",
        (char)0x2066 + "Courses" + (char)0x2069,
        "Cour" + (char)0x00 + "ses",
    };

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public void Constructor_WithAnInvalidName_Throws(string name)
    {
        var error = Assert.Throws<ArgumentException>(() => new Budget(Guid.NewGuid(), name, 200m));

        Assert.Equal("name", error.ParamName);
    }

    [Fact]
    public void Constructor_AcceptsAccentsAndPunctuation()
    {
        Assert.Equal("Épargne & loisirs (été)", new Budget(Guid.NewGuid(), "Épargne & loisirs (été)", 200m).Name);
    }

    [Fact]
    public void Constructor_WithAnAmountBeyondTheBound_Throws()
    {
        Assert.Equal(999_999_999.99m, new Budget(Guid.NewGuid(), "Courses", 999_999_999.99m).PlannedAmount);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Budget(Guid.NewGuid(), "Courses", 1_000_000_000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Constructor_WithAmountNotPositive_Throws(decimal amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Budget(Guid.NewGuid(), "Groceries", amount));
    }
}
