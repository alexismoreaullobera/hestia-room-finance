using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

/// <summary>
/// The recurring lines of the plan through the writer, on a real SQL Server: added, changed, ended, deleted with their
/// overrides, each read back with a new context (the values come from SQL Server).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class FinancePlanWriterLinesTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth May = new(2026, 5);

    private readonly RecurringLine _rent = Expense("Loyer", 650m, day: 5);
    private readonly RecurringLine _water = Expense("Eau", 30m, day: 20);

    [Fact]
    public async Task AddLine_SavesEveryField()
    {
        var options = await SeedAsync();
        var insurance = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Assurance habitation", 180m, Frequency.Yearly, 15,
            new DateOnly(2026, 1, 1), anchorMonth: 3, endDate: new DateOnly(2028, 12, 31), Category.Insurance,
            new SubscriptionInfo(new DateOnly(2026, 12, 31)));

        await WriteAsync(options, w => w.AddLineAsync(insurance, Token));

        AssertSameLine(insurance, (await LoadPlanLinesAsync(options)).Single(l => l.Id == insurance.Id));
    }

    [Theory]
    [InlineData("Loyer")]
    [InlineData("LOYER")]
    public async Task AddLine_UnderANameAlreadyUsed_IsRefusedByTheDatabase_AsANameTaken(string name)
    {
        // Outside the domain, as a second request racing the first: the unique index refuses, whatever the case.
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);
        var writer = new FinancePlanWriter(context);

        var error = await Assert.ThrowsAsync<NameTakenException>(() => writer.AddLineAsync(Expense(name, 700m), Token));
        // The same context: the line refused is not tried again.
        await writer.AddLineAsync(Expense("Box internet", 29.99m), Token);

        Assert.Equal(2601, Assert.IsType<SqlException>(Assert.IsType<DbUpdateException>(error.InnerException).InnerException).Number);
        Assert.Equal(["Box internet", "Eau", "Loyer"], (await LoadPlanLinesAsync(options)).Select(l => l.Name).Order());
    }

    [Fact]
    public async Task UpdateLine_ChangesItEverywhere_KeepingItsOverrides()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_rent.Id, April, 670m), Token));
        var changed = new RecurringLine(_rent.Id, FlowKind.Expense, "Loyer T3", 720m, Frequency.Monthly, 8, LongAgo, category: Category.Housing);

        await WriteAsync(options, w => w.UpdateLineAsync(changed, Token));

        var april = await LoadAsync(options, April);
        AssertSameLine(changed, april.Lines.Single(l => l.Id == _rent.Id));
        Assert.Equal([RecurringOverride.WithAmount(_rent.Id, April, 670m)], april.Overrides);
    }

    [Fact]
    public async Task UpdateLine_EndingIt_RemovesItFromTheMonthsAfter()
    {
        var options = await SeedAsync();

        await WriteAsync(options, w => w.UpdateLineAsync(_rent.EndingOn(April.LastDay), Token));

        Assert.Contains((await LoadAsync(options, April)).Lines, l => l.Id == _rent.Id);
        Assert.DoesNotContain((await LoadAsync(options, May)).Lines, l => l.Id == _rent.Id);
        // Still a line of the plan.
        Assert.Equal(April.LastDay, (await LoadPlanLinesAsync(options)).Single(l => l.Id == _rent.Id).EndDate);
    }

    [Fact]
    public async Task UpdateLine_UnderTheNameOfAnother_IsRefusedByTheDatabase_AndLeavesTheContextClean()
    {
        var options = await SeedAsync();
        await using var context = new FinanceDbContext(options);
        var writer = new FinancePlanWriter(context);

        await Assert.ThrowsAsync<NameTakenException>(() =>
            writer.UpdateLineAsync(new RecurringLine(_rent.Id, FlowKind.Expense, "EAU", 700m, Frequency.Monthly, 5, LongAgo), Token));
        // The same context: the values refused are gone from it, never saved again with the next write.
        await writer.SetLineOverrideAsync(RecurringOverride.Skipped(_rent.Id, May), Token);

        AssertSameLine(_rent, (await LoadPlanLinesAsync(options)).Single(l => l.Id == _rent.Id));
    }

    [Fact]
    public async Task UpdateLine_DeletedMeanwhile_Fails_WithoutCreatingItAgain()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.DeleteLineAsync(_water.Id, 0, Token));

        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(options, w => w.UpdateLineAsync(_water, Token)));

        Assert.DoesNotContain(await LoadPlanLinesAsync(options), l => l.Id == _water.Id);
    }

    [Fact]
    public async Task DeleteLine_DeletesItsOverridesOfEveryMonth_AndOnlyThem_AndCanBeRepeated()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_rent.Id, April, 670m), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(_rent.Id, May), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_water.Id, April, 32m), Token));

        await WriteAsync(options, w => w.DeleteLineAsync(_rent.Id, 2, Token));
        // Deleted: nothing left to count, nor to delete.
        await WriteAsync(options, w => w.DeleteLineAsync(_rent.Id, 0, Token));

        var april = await LoadAsync(options, April);
        Assert.Equal(["Eau"], april.Lines.Select(l => l.Name));
        Assert.Equal([RecurringOverride.WithAmount(_water.Id, April, 32m)], april.Overrides);
        Assert.Empty((await LoadAsync(options, May)).Overrides);
    }

    [Fact]
    public async Task CountOverridesByLine_GivesTheMonthsEnteredPerLine()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.WithAmount(_rent.Id, April, 670m), Token));
        await WriteAsync(options, w => w.SetLineOverrideAsync(RecurringOverride.Skipped(_rent.Id, May), Token));

        await using var context = new FinanceDbContext(options);
        var counts = await new FinancePlanReader(context).CountOverridesByLineAsync(Token);

        // A line without any override is absent: its count is 0.
        Assert.Equal(new Dictionary<Guid, int> { [_rent.Id] = 2 }, counts);
    }

    private async Task<DbContextOptions<FinanceDbContext>> SeedAsync()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        context.RecurringLines.AddRange(_rent.ToRow(), _water.ToRow());
        await context.SaveChangesAsync(Token);
        return options;
    }

    /// <summary>One write, through a new writer and context, as one request would do.</summary>
    private static async Task WriteAsync(DbContextOptions<FinanceDbContext> options, Func<FinancePlanWriter, Task> write)
    {
        await using var context = new FinanceDbContext(options);
        await write(new FinancePlanWriter(context));
    }

    private static async Task<FinanceMonth> LoadAsync(DbContextOptions<FinanceDbContext> options, YearMonth month)
    {
        await using var context = new FinanceDbContext(options);
        return await new FinancePlanReader(context).LoadMonthAsync(month, Token);
    }

    /// <summary>Every line stored, ended ones included, read straight from the table.</summary>
    private static async Task<IReadOnlyList<RecurringLine>> LoadPlanLinesAsync(DbContextOptions<FinanceDbContext> options)
    {
        await using var context = new FinanceDbContext(options);
        var rows = await context.RecurringLines.AsNoTracking().ToListAsync(Token);
        return [.. rows.Select(row => row.ToDomain())];
    }
}
