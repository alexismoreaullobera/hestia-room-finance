using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class RecurringLineRowConfiguration : IEntityTypeConfiguration<RecurringLineRow>
{
    public void Configure(EntityTypeBuilder<RecurringLineRow> line)
    {
        line.ToTable("RecurringLines", table =>
        {
            // Last line of defense if the database is edited by hand: the domain checks the same rules on read.
            table.HasCheckConstraint("CK_RecurringLines_Amount", "[Amount] > 0");
            table.HasCheckConstraint("CK_RecurringLines_DayOfMonth", "[DayOfMonth] BETWEEN 1 AND 31");
            table.HasCheckConstraint("CK_RecurringLines_AnchorMonth", "[AnchorMonth] IS NULL OR [AnchorMonth] BETWEEN 1 AND 12");
        });

        line.HasKey(l => l.Id);
        // Identifiers are created by the application (Guid v7), never by the database.
        line.Property(l => l.Id).ValueGeneratedNever();

        line.Property(l => l.Name).HasMaxLength(100);

        // Enums stored as text: readable in SQL, and robust to a reordering of the enum members.
        line.Property(l => l.Kind).HasConversion<string>().HasMaxLength(20);
        line.Property(l => l.Frequency).HasConversion<string>().HasMaxLength(20);
        line.Property(l => l.Category).HasConversion<string>().HasMaxLength(20);
    }
}
