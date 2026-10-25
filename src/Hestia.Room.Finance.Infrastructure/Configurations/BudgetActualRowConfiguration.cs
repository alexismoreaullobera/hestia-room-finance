using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class BudgetActualRowConfiguration : IEntityTypeConfiguration<BudgetActualRow>
{
    public void Configure(EntityTypeBuilder<BudgetActualRow> actual)
    {
        actual.ToTable("BudgetActuals", table =>
        {
            table.HasCheckConstraint("CK_BudgetActuals_Amount", $"[Amount] BETWEEN 0 AND {AmountBound.Sql}");
            table.HasCheckConstraint("CK_BudgetActuals_Month", "DAY([Month]) = 1");
        });

        // The pair (budget, month) is the identity: two actuals for the same budget and month cannot exist.
        actual.HasKey(a => new { a.BudgetId, a.Month });

        // Deleting a budget deletes its actuals: they have no meaning without it.
        actual.HasOne<BudgetRow>()
            .WithMany()
            .HasForeignKey(a => a.BudgetId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
