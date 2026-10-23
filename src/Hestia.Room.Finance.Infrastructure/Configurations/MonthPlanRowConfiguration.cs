using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

internal sealed class MonthPlanRowConfiguration : IEntityTypeConfiguration<MonthPlanRow>
{
    public void Configure(EntityTypeBuilder<MonthPlanRow> plan)
    {
        plan.ToTable("MonthPlans", table => table.HasCheckConstraint("CK_MonthPlans_Month", "DAY([Month]) = 1"));

        // One row per month: the month itself is the key.
        plan.HasKey(p => p.Month);
    }
}
