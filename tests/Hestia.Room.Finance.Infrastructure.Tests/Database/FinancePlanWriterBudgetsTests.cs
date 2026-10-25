using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

/// <summary>
/// The budgets of the plan through the writer, on a real SQL Server: added, updated, deleted with their actuals, each
/// read back with a new context (the values come from SQL Server).
/// </summary>
[Collection(SqlServerCollection.Name)]
public class FinancePlanWriterBudgetsTests(SqlServerFixture sqlServer)
{
    private static readonly YearMonth May = new(2026, 5);

    private readonly Budget _groceries = NewBudget("Courses", 320m);
    private readonly Budget _outings = NewBudget("Sorties", 120m);

    [Fact]
    public async Task AddBudget_IsInThePlanOfEveryMonth()
    {
        var options = await SeedAsync();
        var fuel = NewBudget("Carburant", 90m);

        await WriteAsync(options, w => w.AddBudgetAsync(fuel, Token));

        var budgets = (await LoadAsync(options, May)).Budgets;
        Assert.Equal(["Carburant", "Courses", "Sorties"], budgets.Select(b => b.Name).Order());
        Assert.Equal(90m, budgets.Single(b => b.Id == fuel.Id).PlannedAmount);
    }

    [Fact]
    public async Task UpdateBudget_RenamesIt_AndChangesItsAmount_KeepingItsActuals()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 301.20m), Token));

        await WriteAsync(options, w => w.UpdateBudgetAsync(new Budget(_groceries.Id, "Alimentation", 350m), Token));

        var month = await LoadAsync(options, April);
        var budget = month.Budgets.Single(b => b.Id == _groceries.Id);
        Assert.Equal(("Alimentation", 350m), (budget.Name, budget.PlannedAmount));
        // Same budget: its actuals stay attached to it.
        Assert.Equal([new BudgetActual(_groceries.Id, April, 301.20m)], month.BudgetActuals);
    }

    [Fact]
    public async Task UpdateBudget_DeletedMeanwhile_Fails_WithoutCreatingItAgain()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.DeleteBudgetAsync(_outings.Id, Token));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WriteAsync(options, w => w.UpdateBudgetAsync(new Budget(_outings.Id, "Sorties", 150m), Token)));

        Assert.DoesNotContain((await LoadAsync(options, April)).Budgets, b => b.Id == _outings.Id);
    }

    [Fact]
    public async Task DeleteBudget_DeletesItsActualsOfEveryMonth_AndOnlyThem_AndCanBeRepeated()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 301.20m), Token));
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, May, 280m), Token));
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_outings.Id, April, 96.50m), Token));

        await WriteAsync(options, w => w.DeleteBudgetAsync(_groceries.Id, Token));
        await WriteAsync(options, w => w.DeleteBudgetAsync(_groceries.Id, Token));

        var april = await LoadAsync(options, April);
        Assert.Equal(["Sorties"], april.Budgets.Select(b => b.Name));
        Assert.Equal([new BudgetActual(_outings.Id, April, 96.50m)], april.BudgetActuals);
        Assert.Empty((await LoadAsync(options, May)).BudgetActuals);
    }

    [Theory]
    [InlineData("Courses")]
    [InlineData("courses")]
    public async Task AddBudget_UnderANameAlreadyUsed_IsRefusedByTheDatabase_AsANameTaken(string name)
    {
        // Outside the domain, as a second request racing the first: the unique index refuses, whatever the case, and
        // the writer says so in the terms of the domain.
        var options = await SeedAsync();

        var error = await Assert.ThrowsAsync<NameTakenException>(() => WriteAsync(options, w => w.AddBudgetAsync(NewBudget(name, 50m), Token)));

        // 2601: duplicate key in a unique index.
        Assert.Equal(2601, Assert.IsType<SqlException>(Assert.IsType<DbUpdateException>(error.InnerException).InnerException).Number);
    }

    [Fact]
    public async Task CountActualsByBudget_GivesTheMonthsEnteredPerBudget()
    {
        var options = await SeedAsync();
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, April, 301.20m), Token));
        await WriteAsync(options, w => w.SetBudgetActualAsync(new BudgetActual(_groceries.Id, May, 280m), Token));

        await using var context = new FinanceDbContext(options);
        var counts = await new FinancePlanReader(context).CountActualsByBudgetAsync(Token);

        // A budget without any actual is absent: its count is 0.
        Assert.Equal(new Dictionary<Guid, int> { [_groceries.Id] = 2 }, counts);
    }

    private async Task<DbContextOptions<FinanceDbContext>> SeedAsync()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        context.Budgets.AddRange(_groceries.ToRow(), _outings.ToRow());
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
}
