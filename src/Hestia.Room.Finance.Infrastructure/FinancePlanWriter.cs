using System.Data;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Configurations;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// Saves the inputs of the user, one use case per method: the counterpart of <see cref="FinancePlanReader"/>, not a
/// generic CRUD layer over each table. The values come as domain objects, so they have passed the rules of their type
/// (cents, bounds, names); the rules involving the rest of the plan or the month are checked by the caller before any
/// write, clears included: through <see cref="PlanBudgets"/> and <see cref="PlanLines"/> (a free name, an existing item)
/// and <see cref="FinanceMonth"/> (a budget of the plan, a line due that month). The database constraints are a further
/// check. The last save wins: the room has a single user. Two first saves of the same key end with the last value. A
/// name taken meanwhile fails with <see cref="NameTakenException"/>; a budget or line updated after its deletion, with an
/// <see cref="InvalidOperationException"/> (a <see cref="DbUpdateConcurrencyException"/> when deleted between the read
/// and the save); a save meeting a concurrent clear, or an actual or override of a budget or line deleted meanwhile, with
/// the database error.
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

    /// <summary>
    /// Adds a budget to the plan. Its name is checked free by <see cref="PlanBudgets"/>; one taken meanwhile is refused
    /// by the unique index, as a <see cref="NameTakenException"/>.
    /// </summary>
    public async Task AddBudgetAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var row = budget.ToRow();
        context.Budgets.Add(row);
        await SaveNamedAsync(row, BudgetRowConfiguration.NameIndex, nameof(budget), cancellationToken);
    }

    /// <summary>
    /// Renames a budget or gives it another planned amount, everywhere (past months included: no versioning). A budget
    /// deleted meanwhile is not created again: the save fails instead. A name taken meanwhile is refused by the unique
    /// index, as a <see cref="NameTakenException"/>.
    /// </summary>
    public async Task UpdateBudgetAsync(Budget budget, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(budget);
        var row = await context.Budgets.FindAsync([budget.Id], cancellationToken)
            ?? throw new InvalidOperationException("The budget was deleted meanwhile.");
        context.Entry(row).CurrentValues.SetValues(budget.ToRow());
        await SaveNamedAsync(row, BudgetRowConfiguration.NameIndex, nameof(budget), cancellationToken);
    }

    /// <summary>
    /// Deletes a budget and, with it, every actual entered for it (the cascade of the foreign key): a budget has no dates,
    /// so this is for a mistake, the view asking for a confirmation that announced <paramref name="actualsEntered"/>
    /// actuals. Nothing is deleted when that number changed (false). Nothing to delete is not an error.
    /// </summary>
    public Task<bool> DeleteBudgetAsync(Guid budgetId, int actualsEntered, CancellationToken cancellationToken = default) =>
        DeleteAsAnnouncedAsync(
            () => context.BudgetActuals.CountAsync(actual => actual.BudgetId == budgetId, cancellationToken),
            actualsEntered,
            () => context.Budgets.Where(budget => budget.Id == budgetId).ExecuteDeleteAsync(cancellationToken),
            cancellationToken);

    /// <summary>
    /// Adds a recurring line to the plan. Its name is checked free by <see cref="PlanLines"/>; one taken meanwhile is
    /// refused by the unique index, as a <see cref="NameTakenException"/>.
    /// </summary>
    public async Task AddLineAsync(RecurringLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        var row = line.ToRow();
        context.RecurringLines.Add(row);
        await SaveNamedAsync(row, RecurringLineRowConfiguration.NameIndex, nameof(line), cancellationToken);
    }

    /// <summary>
    /// Changes a recurring line everywhere (past months included: no versioning), or ends it (the line ended by
    /// <see cref="PlanLines.End"/>). Its overrides stay: one of a month where it no longer falls due is ignored, and counts
    /// again if it falls due that month again. A line deleted meanwhile is not created again: the save fails instead. A
    /// name taken meanwhile is refused by the unique index, as a <see cref="NameTakenException"/>.
    /// </summary>
    public async Task UpdateLineAsync(RecurringLine line, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(line);
        var row = await context.RecurringLines.FindAsync([line.Id], cancellationToken)
            ?? throw new InvalidOperationException("The recurring line was deleted meanwhile.");
        context.Entry(row).CurrentValues.SetValues(line.ToRow());
        await SaveNamedAsync(row, RecurringLineRowConfiguration.NameIndex, nameof(line), cancellationToken);
    }

    /// <summary>
    /// Ends a recurring line (the line ended by <see cref="PlanLines.End"/>): its end date only, never the other columns
    /// as read before, which a change saved meanwhile would get back. A line deleted meanwhile fails with an
    /// <see cref="InvalidOperationException"/>.
    /// </summary>
    public async Task EndLineAsync(Guid lineId, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var ended = await context.RecurringLines
            .Where(line => line.Id == lineId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(line => line.EndDate, endDate), cancellationToken);
        if (ended == 0)
        {
            throw new InvalidOperationException("The recurring line was deleted meanwhile.");
        }
    }

    /// <summary>
    /// Deletes a recurring line and, with it, every override entered for it (the cascade of the foreign key): for a
    /// mistake, the view asking for a confirmation that announced <paramref name="overridesEntered"/> overrides; a line
    /// that stops is ended instead. Nothing is deleted when that number changed (false). Nothing to delete is not an error.
    /// </summary>
    public Task<bool> DeleteLineAsync(Guid lineId, int overridesEntered, CancellationToken cancellationToken = default) =>
        DeleteAsAnnouncedAsync(
            () => context.RecurringOverrides.CountAsync(lineOverride => lineOverride.LineId == lineId, cancellationToken),
            overridesEntered,
            () => context.RecurringLines.Where(line => line.Id == lineId).ExecuteDeleteAsync(cancellationToken),
            cancellationToken);

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

    /// <summary>
    /// Counts what a delete takes with it, then deletes, in one serializable transaction: the count locks the rows it read
    /// and the range around them, so that an actual or override entered meanwhile waits for the delete (then fails on
    /// its foreign key) and never goes unannounced with it. Nothing deleted when the count is not the one announced.
    /// </summary>
    private async Task<bool> DeleteAsAnnouncedAsync(
        Func<Task<int>> count, int announced, Func<Task<int>> delete, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await count() != announced)
        {
            // Disposed without a commit: rolled back, the locks released.
            return false;
        }

        await delete();
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Saves a row added or changed with a name the user typed: a refusal of the unique index on the names becomes a
    /// <see cref="NameTakenException"/>, and the row refused leaves the context, so that a later save in the same context
    /// neither tries it again nor writes its values.
    /// </summary>
    private async Task SaveNamedAsync(object row, string nameIndex, string paramName, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException error) when (IsRefusedBy(error, nameIndex))
        {
            context.Entry(row).State = EntityState.Detached;
            throw new NameTakenException(paramName, error);
        }
    }

    /// <summary>This unique index refused the save: never another unique key, nor another constraint.</summary>
    private static bool IsRefusedBy(DbUpdateException error, string uniqueIndex) =>
        error.InnerException is SqlException { Number: 2601 or 2627 } database
        && database.Message.Contains(uniqueIndex, StringComparison.Ordinal);

    /// <summary>Every column from the domain object, so that a field added later is never left out of an update.</summary>
    private async Task ReplaceAsync<TRow>(TRow row, TRow newRow, CancellationToken cancellationToken)
        where TRow : class
    {
        context.Entry(row).CurrentValues.SetValues(newRow);
        await context.SaveChangesAsync(cancellationToken);
    }
}
