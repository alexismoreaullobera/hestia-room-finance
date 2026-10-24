using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// Saves the inputs of the user, one use case per method: the counterpart of <see cref="FinancePlanReader"/>, not a
/// generic CRUD layer over each table. Every method takes domain objects, so a value reaching the database has passed
/// the domain rules (cents, bounds); the database constraints are the last line of defence.
/// </summary>
public sealed class FinancePlanWriter(FinanceDbContext context)
{
    /// <summary>
    /// Saves the starting balance of a month, creating its settings on the first save. The last save wins: the room has
    /// a single user. Two first saves of the same month at the very same time would make the second one fail (primary
    /// key), never store two rows.
    /// </summary>
    public async Task SetStartingBalanceAsync(MonthPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Tracked on purpose (no AsNoTracking): EF Core detects the change and writes a single UPDATE.
        var row = await context.MonthPlans.FindAsync([plan.Month.FirstDay], cancellationToken);
        if (row is null)
        {
            // A starting balance of 0 € is stored too: a saved 0 is a choice of the user, as explicit as any other.
            context.MonthPlans.Add(plan.ToRow());
        }
        else
        {
            row.StartingBalance = plan.StartingBalance;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
