using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class BudgetRowConfiguration : IEntityTypeConfiguration<BudgetRow>
{
    public void Configure(EntityTypeBuilder<BudgetRow> budget)
    {
        budget.ToTable("Budgets", table => table.HasCheckConstraint("CK_Budgets_PlannedAmount", "[PlannedAmount] > 0"));

        budget.HasKey(b => b.Id);
        budget.Property(b => b.Id).ValueGeneratedNever();
        budget.Property(b => b.Name).HasMaxLength(100);
    }
}
