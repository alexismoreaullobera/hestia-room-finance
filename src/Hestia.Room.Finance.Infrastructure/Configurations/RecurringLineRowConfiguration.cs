using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class RecurringLineRowConfiguration : IEntityTypeConfiguration<RecurringLineRow>
{
    /// <summary>The unique index on the names: the writer tells its refusal apart from any other by this name.</summary>
    public const string NameIndex = "IX_RecurringLines_Name";

    public void Configure(EntityTypeBuilder<RecurringLineRow> line)
    {
        line.ToTable("RecurringLines", table =>
        {
            // Last line of defense if the database is edited by hand: the domain checks the same rules on read.
            table.HasCheckConstraint("CK_RecurringLines_Amount", $"[Amount] > 0 AND [Amount] <= {AmountBound.Sql}");
            table.HasCheckConstraint("CK_RecurringLines_DayOfMonth", "[DayOfMonth] BETWEEN 1 AND 31");
            table.HasCheckConstraint("CK_RecurringLines_AnchorMonth", "[AnchorMonth] IS NULL OR [AnchorMonth] BETWEEN 1 AND 12");
            table.HasCheckConstraint("CK_RecurringLines_EndDate", "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
        });

        line.HasKey(l => l.Id);
        // Identifiers are created by the application (Guid v7), never by the database.
        line.Property(l => l.Id).ValueGeneratedNever();

        line.Property(l => l.Name).HasMaxLength(NameLimit.MaxLength).UseCollation(NameColumn.Collation);
        // Two lines never share a name at the same time: checked by the domain (PlanLines), and here in the last resort for
        // the lines without end, which always run together (an index cannot compare two periods).
        line.HasIndex(l => l.Name).IsUnique().HasFilter("[EndDate] IS NULL").HasDatabaseName(NameIndex);

        // Enums stored as text: readable in SQL, and robust to a reordering of the enum members.
        line.Property(l => l.Kind).HasConversion<string>().HasMaxLength(20);
        line.Property(l => l.Frequency).HasConversion<string>().HasMaxLength(20);
        line.Property(l => l.Category).HasConversion<string>().HasMaxLength(20);
    }
}
