using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// Saves the inputs of the user, one use case per method: the counterpart of <see cref="FinancePlanReader"/>, not a
/// generic CRUD layer over each table. Every method takes domain objects, so a value reaching the database has passed
/// the domain rules (cents, bounds); the database constraints are a further check.
/// </summary>
public sealed class FinancePlanWriter(FinanceDbContext context)
{
    /// <summary>
    /// Saves the starting balance of a month, creating its settings on the first save. The last save wins: the room has
    /// a single user, and two first saves of the same month at the same time (two hosts, or a host retrying this
    /// idempotent input) end with the value of the last one, never with an error or two rows.
    /// </summary>
    public async Task SetStartingBalanceAsync(MonthPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var newRow = plan.ToRow();

        // Find tracks the row it returns: EF Core then writes a single UPDATE, or none when nothing changed.
        var row = await context.MonthPlans.FindAsync([newRow.Month], cancellationToken);
        if (row is not null)
        {
            await ReplaceAsync(row, newRow, cancellationToken);
            return;
        }

        // A starting balance of 0 € is stored too: a saved 0 is a choice of the user, as explicit as any other.
        context.MonthPlans.Add(newRow);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2627 or 2601 })
        {
            // Another first save of this month inserted its row in between: this one applies on top of it.
            context.ChangeTracker.Clear();
            var inserted = await context.MonthPlans.FindAsync([newRow.Month], cancellationToken)
                ?? throw new InvalidOperationException($"The settings of {plan.Month} were inserted, then deleted.", error);
            await ReplaceAsync(inserted, newRow, cancellationToken);
        }
    }

    /// <summary>Every column from the domain object, so that a field added later is never left out of an update.</summary>
    private async Task ReplaceAsync(MonthPlanRow row, MonthPlanRow newRow, CancellationToken cancellationToken)
    {
        context.Entry(row).CurrentValues.SetValues(newRow);
        await context.SaveChangesAsync(cancellationToken);
    }
}
