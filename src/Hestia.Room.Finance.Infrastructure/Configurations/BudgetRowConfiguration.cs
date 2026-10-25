using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class BudgetRowConfiguration : IEntityTypeConfiguration<BudgetRow>
{
    /// <summary>The unique index on the names: the writer tells its refusal apart from any other by this name.</summary>
    public const string NameIndex = "IX_Budgets_Name";

    public void Configure(EntityTypeBuilder<BudgetRow> budget)
    {
        budget.ToTable("Budgets", table => table.HasCheckConstraint("CK_Budgets_PlannedAmount",
            $"[PlannedAmount] > 0 AND [PlannedAmount] <= {AmountBound.Sql}"));

        budget.HasKey(b => b.Id);
        budget.Property(b => b.Id).ValueGeneratedNever();
        budget.Property(b => b.Name).HasMaxLength(NameLimit.MaxLength).UseCollation(NameColumn.Collation);
        // Two budgets never share a name: checked by the domain (PlanBudgets), and here in the last resort.
        budget.HasIndex(b => b.Name).IsUnique().HasDatabaseName(NameIndex);
    }
}
