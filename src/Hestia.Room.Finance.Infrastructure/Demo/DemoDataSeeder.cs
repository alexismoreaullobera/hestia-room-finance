using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure.Demo;

/// <summary>
/// Saves the <see cref="DemoPlan"/> into an empty database. Never touches a database that already has a plan:
/// real data is never mixed with, nor overwritten by, demo data.
/// </summary>
public sealed class DemoDataSeeder(FinanceDbContext context)
{
    /// <summary>Seeds the demo plan if the database is empty. Returns true if it did.</summary>
    public async Task<bool> SeedIfEmptyAsync(YearMonth currentMonth, CancellationToken cancellationToken = default)
    {
        var hasPlan = await context.RecurringLines.AnyAsync(cancellationToken)
            || await context.Budgets.AnyAsync(cancellationToken);
        if (hasPlan)
        {
            return false;
        }

        var demo = DemoPlan.For(currentMonth);
        context.RecurringLines.AddRange(demo.Lines.Select(line => line.ToRow()));
        context.Budgets.AddRange(demo.Budgets.Select(budget => budget.ToRow()));
        context.MonthPlans.AddRange(demo.MonthPlans.Select(plan => plan.ToRow()));
        context.BudgetActuals.AddRange(demo.BudgetActuals.Select(actual => actual.ToRow()));
        context.RecurringOverrides.AddRange(demo.Overrides.Select(correction => correction.ToRow()));

        // One SaveChanges: the whole demo plan is saved in a single transaction, or not at all.
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
