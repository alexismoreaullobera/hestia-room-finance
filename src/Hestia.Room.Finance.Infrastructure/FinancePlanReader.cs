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
            .Where(correction => correction.Month == firstDay)
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
