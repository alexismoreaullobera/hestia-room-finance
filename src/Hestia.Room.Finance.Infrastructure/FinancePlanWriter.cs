using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// Saves the inputs of the user, one use case per method: the counterpart of <see cref="FinancePlanReader"/>, not a
/// generic CRUD layer over each table. The values come as domain objects, so they have passed the rules of their type
/// (cents, bounds); whether a budget belongs to the month or a line is due that month is checked by the caller through
/// <see cref="FinanceMonth"/>, before any write, clears included. The database constraints are a further check.
/// The last save wins: the room has a single user. Two first saves of the same key end with the last value; a save
/// meeting a concurrent clear, or a budget or line deleted meanwhile, fails with the database error instead.
/// </summary>
public sealed class FinancePlanWriter(FinanceDbContext context)
{
    /// <summary>
    /// Saves the starting balance of a month, creating its settings on the first save. A starting balance of 0 € is
    /// stored too: a saved 0 is a choice of the user, as explicit as any other.
    /// </summary>
    public Task SetStartingBalanceAsync(MonthPlan plan, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var row = plan.ToRow();
        return UpsertAsync(context.MonthPlans, [row.Month], row, cancellationToken);
    }

    /// <summary>Saves the actual total of a budget for a month, replacing the one entered before.</summary>
    public Task SetBudgetActualAsync(BudgetActual actual, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actual);
        var row = actual.ToRow();
        return UpsertAsync(context.BudgetActuals, [row.BudgetId, row.Month], row, cancellationToken);
    }

    /// <summary>
    /// Removes the actual of a budget for a month: the budget is then considered spent as planned. Nothing to remove is
    /// not an error (the same call twice, or a budget never entered).
    /// </summary>
    public Task ClearBudgetActualAsync(Guid budgetId, YearMonth month, CancellationToken cancellationToken = default)
    {
        var firstDay = month.FirstDay;
        return context.BudgetActuals
            .Where(actual => actual.BudgetId == budgetId && actual.Month == firstDay)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>Saves the override of a recurring line for a month (another amount, or skipped), replacing the one before.</summary>
    public Task SetLineOverrideAsync(RecurringOverride lineOverride, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lineOverride);
        var row = lineOverride.ToRow();
        return UpsertAsync(context.RecurringOverrides, [row.LineId, row.Month], row, cancellationToken);
    }

    /// <summary>
    /// Removes the override of a recurring line for a month: the line is then considered paid as planned. Nothing to
    /// remove is not an error.
    /// </summary>
    public Task ClearLineOverrideAsync(Guid lineId, YearMonth month, CancellationToken cancellationToken = default)
    {
        var firstDay = month.FirstDay;
        return context.RecurringOverrides
            .Where(lineOverride => lineOverride.LineId == lineId && lineOverride.Month == firstDay)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Inserts the row, or replaces every column of the existing one with the same key. Two first saves of the same key
    /// at the same time (two hosts, or a host retrying an idempotent input) end with the value of the last one, never
    /// with an error or two rows.
    /// </summary>
    private async Task UpsertAsync<TRow>(DbSet<TRow> rows, object[] key, TRow newRow, CancellationToken cancellationToken)
        where TRow : class
    {
        // Find tracks the row it returns: EF Core then writes a single UPDATE, or none when nothing changed.
        var row = await rows.FindAsync(key, cancellationToken);
        if (row is not null)
        {
            await ReplaceAsync(row, newRow, cancellationToken);
            return;
        }

        rows.Add(newRow);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2627 or 2601 })
        {
            // Another first save of this key inserted its row in between: this one applies on top of it. Only the row
            // added here is detached, never what the caller tracks in the same context.
            context.Entry(newRow).State = EntityState.Detached;
            var inserted = await rows.FindAsync(key, cancellationToken)
                ?? throw new InvalidOperationException("A row was inserted by another save, then deleted.", error);
            await ReplaceAsync(inserted, newRow, cancellationToken);
        }
    }

    /// <summary>Every column from the domain object, so that a field added later is never left out of an update.</summary>
    private async Task ReplaceAsync<TRow>(TRow row, TRow newRow, CancellationToken cancellationToken)
        where TRow : class
    {
        context.Entry(row).CurrentValues.SetValues(newRow);
        await context.SaveChangesAsync(cancellationToken);
    }
}
