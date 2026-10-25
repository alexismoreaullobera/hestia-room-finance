using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Rows;
using Microsoft.EntityFrameworkCore;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// Reads one month of the plan from the database and returns it as domain objects, ready for the calculations.
/// A use-case reader (repository side), not a generic CRUD layer over each table: the caller asks for "April",
/// never for rows.
/// </summary>
public sealed class FinancePlanReader(FinanceDbContext context)
{
    /// <summary>
    /// For each budget with actuals, the number of months they were entered for: what deleting it would also delete.
    /// A budget without any actual is absent (0).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> CountActualsByBudgetAsync(CancellationToken cancellationToken = default) =>
        await context.BudgetActuals.AsNoTracking()
            .GroupBy(actual => actual.BudgetId)
            .Select(group => new { BudgetId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.BudgetId, entry => entry.Count, cancellationToken);

    /// <summary>
    /// For each recurring line with overrides, the number of months they were entered for: what deleting it would also
    /// delete. A line without any override is absent (0).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, int>> CountOverridesByLineAsync(CancellationToken cancellationToken = default) =>
        await context.RecurringOverrides.AsNoTracking()
            .GroupBy(lineOverride => lineOverride.LineId)
            .Select(group => new { LineId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(entry => entry.LineId, entry => entry.Count, cancellationToken);

    /// <summary>
    /// The whole plan, outside of any month: every budget and every recurring line, ended and future ones included. What
    /// a change of the plan is checked against; the calculations of a month read <see cref="LoadMonthAsync"/>.
    /// </summary>
    public async Task<FinancePlan> LoadPlanAsync(CancellationToken cancellationToken = default)
    {
        var budgets = await context.Budgets.AsNoTracking().ToListAsync(cancellationToken);
        var lines = await context.RecurringLines.AsNoTracking().ToListAsync(cancellationToken);

        return new FinancePlan(budgets.Select(row => row.ToDomain()), lines.Select(row => row.ToDomain()));
    }

    public async Task<FinanceMonth> LoadMonthAsync(YearMonth month, CancellationToken cancellationToken = default)
    {
        var firstDay = month.FirstDay;
        var lastDay = month.LastDay;

        // Read-only queries: AsNoTracking skips the change tracker (no snapshot copies, nothing to save).
        // The queries run one after the other: a DbContext does not support concurrent operations.
        var lines = await context.RecurringLines.AsNoTracking()
            // Same rule as RecurringLine.IsActiveIn, computed by SQL Server: lines that run during the month.
            .Where(line => line.StartDate <= lastDay && (line.EndDate == null || line.EndDate >= firstDay))
            .ToListAsync(cancellationToken);

        var budgets = await context.Budgets.AsNoTracking().ToListAsync(cancellationToken);

        var actuals = await context.BudgetActuals.AsNoTracking()
            .Where(actual => actual.Month == firstDay)
            .ToListAsync(cancellationToken);

        var overrides = await context.RecurringOverrides.AsNoTracking()
            .Where(lineOverride => lineOverride.Month == firstDay)
            .ToListAsync(cancellationToken);

        var plan = await context.MonthPlans.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Month == firstDay, cancellationToken);

        return new FinanceMonth(
            // No settings saved for this month: the starting balance is 0 €, as in the scope.
            plan?.ToDomain() ?? new MonthPlan(month),
            lines.Select(row => row.ToDomain()),
            budgets.Select(row => row.ToDomain()),
            actuals.Select(row => row.ToDomain()),
            overrides.Select(row => row.ToDomain()));
    }
}
