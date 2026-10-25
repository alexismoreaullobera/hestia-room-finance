using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class MonthPlanRowConfiguration : IEntityTypeConfiguration<MonthPlanRow>
{
    public void Configure(EntityTypeBuilder<MonthPlanRow> plan)
    {
        plan.ToTable("MonthPlans", table =>
        {
            table.HasCheckConstraint("CK_MonthPlans_Month", "DAY([Month]) = 1");
            // The starting balance may be negative (an overdrawn account), within the bound either way.
            table.HasCheckConstraint("CK_MonthPlans_StartingBalance",
                $"[StartingBalance] BETWEEN -{AmountBound.Sql} AND {AmountBound.Sql}");
        });

        // One row per month: the month itself is the key.
        plan.HasKey(p => p.Month);
    }
}
