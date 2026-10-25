using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Hestia.Room.Finance.Infrastructure.Tests;

/// <summary>
/// Checks the schema produced by the EF Core configuration, without any database: EF Core builds the model and
/// generates the SQL creation script offline.
/// </summary>
public class FinanceDbContextModelTests
{
    private static readonly IModel Model;
    private static readonly string CreateScript;

    static FinanceDbContextModelTests()
    {
        // The connection string is never opened: the model and the script are built offline.
        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseRoomSqlServer("Server=offline").Options;
        using var context = new FinanceDbContext(options);
        Model = context.GetService<IDesignTimeModel>().Model;
        CreateScript = context.Database.GenerateCreateScript();
    }

    [Fact]
    public void EveryTable_IsInTheFinanceSchema()
    {
        Assert.All(Model.GetEntityTypes(), entity => Assert.Equal("finance", entity.GetSchema()));
    }

    [Theory]
    [InlineData(typeof(RecurringLineRow), nameof(RecurringLineRow.Amount))]
    [InlineData(typeof(BudgetRow), nameof(BudgetRow.PlannedAmount))]
    [InlineData(typeof(MonthPlanRow), nameof(MonthPlanRow.StartingBalance))]
    [InlineData(typeof(BudgetActualRow), nameof(BudgetActualRow.Amount))]
    [InlineData(typeof(RecurringOverrideRow), nameof(RecurringOverrideRow.ActualAmount))]
    public void Amounts_AreStoredAsDecimal18Comma2(Type rowType, string property)
    {
        var amount = Property(rowType, property);

        // decimal(18,2) is also EF Core's silent default on SQL Server: checking the configured precision makes sure
        // it is our explicit choice, not a coincidence (EF Core warns about truncation when nothing is configured).
        Assert.Equal(18, amount.GetPrecision());
        Assert.Equal(2, amount.GetScale());
        Assert.Equal("decimal(18,2)", amount.GetColumnType());
    }

    [Fact]
    public void BudgetNames_HaveTheCollationOfTheDomain_WhateverTheDatabase()
    {
        // Case insensitive, accent sensitive: never the default collation of a database created elsewhere.
        var name = Property(typeof(BudgetRow), nameof(BudgetRow.Name));

        Assert.Equal("SQL_Latin1_General_CP1_CI_AS", name.GetCollation());
        Assert.Equal(100, name.GetMaxLength());
    }

    [Theory]
    [InlineData(nameof(RecurringLineRow.Kind))]
    [InlineData(nameof(RecurringLineRow.Frequency))]
    [InlineData(nameof(RecurringLineRow.Category))]
    public void Enums_AreStoredAsText(string property)
    {
        var column = Property(typeof(RecurringLineRow), property);

        Assert.Equal(typeof(string), column.GetProviderClrType());
        Assert.Equal(20, column.GetMaxLength());
    }

    [Fact]
    public void Identifiers_AreNeverGeneratedByTheDatabase()
    {
        Assert.Equal(ValueGenerated.Never, Property(typeof(RecurringLineRow), nameof(RecurringLineRow.Id)).ValueGenerated);
        Assert.Equal(ValueGenerated.Never, Property(typeof(BudgetRow), nameof(BudgetRow.Id)).ValueGenerated);
    }

    [Theory]
    [InlineData(typeof(BudgetActualRow), nameof(BudgetActualRow.BudgetId), nameof(BudgetActualRow.Month))]
    [InlineData(typeof(RecurringOverrideRow), nameof(RecurringOverrideRow.LineId), nameof(RecurringOverrideRow.Month))]
    public void ActualsAndOverrides_AreIdentifiedByTheirPairWithTheMonth(Type rowType, string first, string second)
    {
        var key = Model.FindEntityType(rowType)!.FindPrimaryKey()!;

        Assert.Equal([first, second], key.Properties.Select(p => p.Name));
    }

    [Theory]
    [InlineData(typeof(BudgetActualRow))]
    [InlineData(typeof(RecurringOverrideRow))]
    public void ActualsAndOverrides_AreDeletedWithTheirParent(Type rowType)
    {
        var foreignKey = Assert.Single(Model.FindEntityType(rowType)!.GetForeignKeys());

        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
    }

    [Theory]
    [InlineData("CK_RecurringLines_Amount")]
    [InlineData("CK_RecurringLines_DayOfMonth")]
    [InlineData("CK_RecurringLines_AnchorMonth")]
    [InlineData("CK_Budgets_PlannedAmount")]
    [InlineData("CK_MonthPlans_Month")]
    [InlineData("CK_MonthPlans_StartingBalance")]
    [InlineData("CK_BudgetActuals_Amount")]
    [InlineData("CK_BudgetActuals_Month")]
    [InlineData("CK_RecurringOverrides_ActualAmount")]
    [InlineData("CK_RecurringOverrides_Month")]
    public void CheckConstraints_AreInTheCreationScript(string constraint)
    {
        Assert.Contains($"CONSTRAINT [{constraint}] CHECK", CreateScript);
    }

    [Fact]
    public void BudgetNames_AreUnique_InTheCreationScript()
    {
        // The last resort of the rule of PlanBudgets: two budgets never share a name.
        Assert.Contains("CREATE UNIQUE INDEX [IX_Budgets_Name] ON [finance].[Budgets] ([Name])", CreateScript);
    }

    [Fact]
    public void MigrationsHistory_IsInTheFinanceSchema()
    {
        // Several rooms may share a database: each keeps its own migrations history.
        Assert.Contains("CREATE TABLE [finance].[__EFMigrationsHistory]", CreateScriptWithHistory());
    }

    [Fact]
    public void Migrations_AreUpToDateWithTheModel()
    {
        // Fails when the configuration changes without a new migration: run `dotnet ef migrations add <Name>`.
        using var context = OfflineContext();

        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Migrations_StartWithInitialCreate()
    {
        using var context = OfflineContext();

        Assert.EndsWith("_InitialCreate", context.Database.GetMigrations().First());
    }

    private static string CreateScriptWithHistory()
    {
        using var context = OfflineContext();
        var migrator = context.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();
        return migrator.GenerateScript();
    }

    private static FinanceDbContext OfflineContext() =>
        new(new DbContextOptionsBuilder<FinanceDbContext>().UseRoomSqlServer("Server=offline").Options);

    private static IProperty Property(Type rowType, string name) => Model.FindEntityType(rowType)!.FindProperty(name)!;
}
