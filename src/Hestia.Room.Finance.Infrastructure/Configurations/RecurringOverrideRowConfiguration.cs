using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class RecurringOverrideRowConfiguration : IEntityTypeConfiguration<RecurringOverrideRow>
{
    public void Configure(EntityTypeBuilder<RecurringOverrideRow> correction)
    {
        correction.ToTable("RecurringOverrides", table =>
        {
            // Null means "skipped this month"; otherwise the actual amount is positive.
            table.HasCheckConstraint("CK_RecurringOverrides_ActualAmount", "[ActualAmount] IS NULL OR [ActualAmount] > 0");
            table.HasCheckConstraint("CK_RecurringOverrides_Month", "DAY([Month]) = 1");
        });

        // The pair (line, month) is the identity: two overrides for the same line and month cannot exist.
        correction.HasKey(o => new { o.LineId, o.Month });

        // Deleting a recurring line deletes its overrides.
        correction.HasOne<RecurringLineRow>()
            .WithMany()
            .HasForeignKey(o => o.LineId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
