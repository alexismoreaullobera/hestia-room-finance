using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// EF Core context of the Finance room. Each DbSet is a table of persistence rows (never domain objects):
/// conversions happen in <see cref="RowMapping"/>.
/// </summary>
public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options)
{
    /// <summary>Every table of the room lives in this schema, apart from other rooms sharing a database.</summary>
    public const string Schema = "finance";

    public DbSet<RecurringLineRow> RecurringLines => Set<RecurringLineRow>();

    public DbSet<BudgetRow> Budgets => Set<BudgetRow>();

    public DbSet<MonthPlanRow> MonthPlans => Set<MonthPlanRow>();

    public DbSet<BudgetActualRow> BudgetActuals => Set<BudgetActualRow>();

    public DbSet<RecurringOverrideRow> RecurringOverrides => Set<RecurringOverrideRow>();

    /// <summary>
    /// The SQL Server settings of the room, shared by every place that creates the context (application, design-time
    /// factory, tests): the migrations history table also lives in the room schema, so that several rooms sharing a
    /// database never mix their migration histories.
    /// </summary>
    public static DbContextOptionsBuilder<FinanceDbContext> UseRoomSqlServer(
        DbContextOptionsBuilder<FinanceDbContext> options, string connectionString) =>
        options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", Schema));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        // One configuration class per table, in the Configurations folder.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Every amount of the room is in euros, with cents: decimal(18,2) by default, no per-column repetition.
        configurationBuilder.Properties<decimal>().HavePrecision(18, 2);
    }
}
