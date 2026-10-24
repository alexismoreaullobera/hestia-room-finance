using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using static Hestia.Room.Finance.Infrastructure.Tests.InfraTestData;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

/// <summary>The schema and the mapping on a real SQL Server: what the offline model tests cannot see.</summary>
[Collection(SqlServerCollection.Name)]
public class DatabaseTests(SqlServerFixture sqlServer)
{
    [Fact]
    public async Task Migrations_ApplyOnAnEmptyDatabase_WithTheHistoryInTheFinanceSchema()
    {
        await using var context = new FinanceDbContext(await sqlServer.CreateMigratedDatabaseAsync());

        Assert.Empty(await context.Database.GetPendingMigrationsAsync(Token));
        var historyTables = await context.Database
            .SqlQuery<string>($"SELECT s.name AS [Value] FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id WHERE t.name = '__EFMigrationsHistory'")
            .ToListAsync(Token);
        Assert.Equal(["finance"], historyTables);
    }

    [Fact]
    public async Task EveryEntity_SurvivesARoundTripThroughTheDatabase()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        // Accented names: an ASCII-only (non Unicode) column would lose them.
        var line = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Assurance téléphone", 15.99m, Frequency.Quarterly,
            dayOfMonth: 31, startDate: new DateOnly(2025, 1, 15), anchorMonth: 2, endDate: new DateOnly(2027, 1, 31),
            category: Category.Telecom, subscription: new SubscriptionInfo(new DateOnly(2026, 12, 31)));
        var budget = NewBudget("Épicerie", 212.40m);
        var plan = new MonthPlan(April, -30.50m);
        var actual = new BudgetActual(budget.Id, April, 0m);
        var skipped = RecurringOverride.Skipped(line.Id, April);
        var corrected = RecurringOverride.WithAmount(line.Id, new YearMonth(2026, 5), 17.20m);

        await using (var write = new FinanceDbContext(options))
        {
            write.RecurringLines.Add(line.ToRow());
            write.Budgets.Add(budget.ToRow());
            write.MonthPlans.Add(plan.ToRow());
            write.BudgetActuals.Add(actual.ToRow());
            write.RecurringOverrides.AddRange(skipped.ToRow(), corrected.ToRow());
            await write.SaveChangesAsync(Token);
        }

        // A new context: the values come back from SQL Server, not from EF Core's memory.
        await using var read = new FinanceDbContext(options);
        AssertSameLine(line, (await read.RecurringLines.SingleAsync(Token)).ToDomain());
        var budgetBack = (await read.Budgets.SingleAsync(Token)).ToDomain();
        Assert.Equal((budget.Id, budget.Name, budget.PlannedAmount), (budgetBack.Id, budgetBack.Name, budgetBack.PlannedAmount));
        Assert.Equal(plan, (await read.MonthPlans.SingleAsync(Token)).ToDomain());
        Assert.Equal(actual, (await read.BudgetActuals.SingleAsync(Token)).ToDomain());
        Assert.Equal([skipped, corrected], (await read.RecurringOverrides.OrderBy(o => o.Month).ToListAsync(Token)).Select(o => o.ToDomain()));
    }

    [Fact]
    public async Task Enums_AreStoredAsText()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        await using var context = new FinanceDbContext(options);
        context.RecurringLines.Add(new RecurringLine(Guid.CreateVersion7(), FlowKind.Income, "Salaire", 2000m, Frequency.Monthly, 28, new DateOnly(2025, 1, 1)).ToRow());
        await context.SaveChangesAsync(Token);

        var kinds = await context.Database.SqlQuery<string>($"SELECT Kind AS [Value] FROM finance.RecurringLines").ToListAsync(Token);

        Assert.Equal(["Income"], kinds);
    }

    [Fact]
    public async Task TwoActualsForTheSameBudgetAndMonth_AreRejected()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        var budget = NewBudget("Courses", 200m);
        await using (var first = new FinanceDbContext(options))
        {
            first.Budgets.Add(budget.ToRow());
            first.BudgetActuals.Add(new BudgetActual(budget.Id, April, 100m).ToRow());
            await first.SaveChangesAsync(Token);
        }

        // A separate context, as two separate requests would do: the database itself must refuse the duplicate.
        await using var second = new FinanceDbContext(options);
        second.BudgetActuals.Add(new BudgetActual(budget.Id, April, 120m).ToRow());

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync(Token));

        // 2627: primary key violation, and not any other failure of the save.
        Assert.Equal(2627, Assert.IsType<SqlException>(error.InnerException).Number);
    }

    [Theory]
    [InlineData("INSERT INTO finance.Budgets (Id, Name, PlannedAmount) VALUES (NEWID(), 'Courses', -200)",
        "CK_Budgets_PlannedAmount")]
    [InlineData("INSERT INTO finance.RecurringLines (Id, Kind, Name, Amount, Frequency, DayOfMonth, StartDate, Category, IsSubscription) "
        + "VALUES (NEWID(), 'Expense', 'Loyer', 650, 'Monthly', 32, '2025-01-01', 'Housing', 0)",
        "CK_RecurringLines_DayOfMonth")]
    [InlineData("INSERT INTO finance.MonthPlans (Month, StartingBalance) VALUES ('2026-04-15', 0)",
        "CK_MonthPlans_Month")]
    // The amount bound of the domain (999 999 999,99 either way) on the amounts the user types.
    [InlineData("INSERT INTO finance.MonthPlans (Month, StartingBalance) VALUES ('2026-04-01', -1000000000)",
        "CK_MonthPlans_StartingBalance")]
    [InlineData("INSERT INTO finance.MonthPlans (Month, StartingBalance) VALUES ('2026-04-01', 1000000000)",
        "CK_MonthPlans_StartingBalance")]
    // The rules the bound migration re-created: an actual is never negative, a correction never 0 nor negative.
    [InlineData("INSERT INTO finance.Budgets (Id, Name, PlannedAmount) VALUES (NEWID(), 'Courses', 200); "
        + "INSERT INTO finance.BudgetActuals (BudgetId, Month, Amount) SELECT Id, '2026-04-01', -1 FROM finance.Budgets",
        "CK_BudgetActuals_Amount")]
    [InlineData("INSERT INTO finance.RecurringLines (Id, Kind, Name, Amount, Frequency, DayOfMonth, StartDate, Category, IsSubscription) "
        + "VALUES (NEWID(), 'Expense', 'Loyer', 650, 'Monthly', 5, '2025-01-01', 'Housing', 0); "
        + "INSERT INTO finance.RecurringOverrides (LineId, Month, ActualAmount) SELECT Id, '2026-04-01', 0 FROM finance.RecurringLines",
        "CK_RecurringOverrides_ActualAmount")]
    [InlineData("INSERT INTO finance.RecurringLines (Id, Kind, Name, Amount, Frequency, DayOfMonth, StartDate, Category, IsSubscription) "
        + "VALUES (NEWID(), 'Expense', 'Loyer', 650, 'Monthly', 5, '2025-01-01', 'Housing', 0); "
        + "INSERT INTO finance.RecurringOverrides (LineId, Month, ActualAmount) SELECT Id, '2026-04-01', -1 FROM finance.RecurringLines",
        "CK_RecurringOverrides_ActualAmount")]
    [InlineData("INSERT INTO finance.Budgets (Id, Name, PlannedAmount) VALUES (NEWID(), 'Courses', 200); "
        + "INSERT INTO finance.BudgetActuals (BudgetId, Month, Amount) SELECT Id, '2026-04-01', 1000000000 FROM finance.Budgets",
        "CK_BudgetActuals_Amount")]
    [InlineData("INSERT INTO finance.RecurringLines (Id, Kind, Name, Amount, Frequency, DayOfMonth, StartDate, Category, IsSubscription) "
        + "VALUES (NEWID(), 'Expense', 'Loyer', 650, 'Monthly', 5, '2025-01-01', 'Housing', 0); "
        + "INSERT INTO finance.RecurringOverrides (LineId, Month, ActualAmount) SELECT Id, '2026-04-01', 1000000000 FROM finance.RecurringLines",
        "CK_RecurringOverrides_ActualAmount")]
    public async Task CheckConstraints_RejectAValueWrittenOutsideTheDomain(string insert, string constraint)
    {
        await using var context = new FinanceDbContext(await sqlServer.CreateMigratedDatabaseAsync());

        // Bypasses the domain on purpose, as a manual edit of the database would. Constant SQL, no user input.
        var error = await Assert.ThrowsAsync<SqlException>(() => context.Database.ExecuteSqlRawAsync(insert, Token));

        // 547: constraint violation; the message names which one.
        Assert.Equal(547, error.Number);
        Assert.Contains(constraint, error.Message);
    }

    [Fact]
    public async Task DeletingABudget_DeletesItsActuals()
    {
        var options = await sqlServer.CreateMigratedDatabaseAsync();
        var budget = NewBudget("Courses", 200m);
        await using (var write = new FinanceDbContext(options))
        {
            write.Budgets.Add(budget.ToRow());
            write.BudgetActuals.Add(new BudgetActual(budget.Id, April, 100m).ToRow());
            await write.SaveChangesAsync(Token);
        }

        await using var context = new FinanceDbContext(options);
        await context.Budgets.Where(b => b.Id == budget.Id).ExecuteDeleteAsync(Token);

        Assert.False(await context.BudgetActuals.AnyAsync(Token));
    }
}
