using Hestia.Room.Finance.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// Everything an input tool can write (month settings, budget actuals, line overrides, budgets), read straight from the
/// database: compared before and after a refused call, it shows that nothing was written anywhere, a row at 0 € and
/// the demo month included. The tests of a collection run one after the other, so no other write interleaves.
/// </summary>
internal static class WrittenRows
{
    /// <summary>The first month the input tests write to, years after the demo plan: no other test reads it.</summary>
    public static readonly DateOnly FirstTestMonth = new(2031, 1, 1);

    /// <summary>The start of the name of every budget an input test creates: the budgets of the demo never have it.</summary>
    public const string TestBudgetPrefix = "Test budget ";

    /// <summary>
    /// Deletes what the input tests wrote: their months (settings, actuals, overrides) and their budgets (with their
    /// actuals), so that what other classes read (the budgets of the plan, the number of actuals of a budget, the fixtures)
    /// never depends on the order of the tests.
    /// </summary>
    public static async Task DeleteTestWritesAsync(McpServerFixture mcp)
    {
        await using var scope = mcp.Server.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var token = TestContext.Current.CancellationToken;

        await context.MonthPlans.Where(p => p.Month >= FirstTestMonth).ExecuteDeleteAsync(token);
        await context.BudgetActuals.Where(a => a.Month >= FirstTestMonth).ExecuteDeleteAsync(token);
        await context.RecurringOverrides.Where(o => o.Month >= FirstTestMonth).ExecuteDeleteAsync(token);
        await context.Budgets.Where(b => b.Name.StartsWith(TestBudgetPrefix)).ExecuteDeleteAsync(token);
    }

    public static async Task<string> SnapshotAsync(McpServerFixture mcp)
    {
        await using var scope = mcp.Server.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var token = TestContext.Current.CancellationToken;

        var plans = await context.MonthPlans.AsNoTracking().OrderBy(p => p.Month).ToListAsync(token);
        var actuals = await context.BudgetActuals.AsNoTracking().OrderBy(a => a.Month).ThenBy(a => a.BudgetId).ToListAsync(token);
        var overrides = await context.RecurringOverrides.AsNoTracking().OrderBy(o => o.Month).ThenBy(o => o.LineId).ToListAsync(token);
        var budgets = await context.Budgets.AsNoTracking().OrderBy(b => b.Id).ToListAsync(token);

        return string.Join("\n",
            plans.Select(p => $"plan {p.Month} {p.StartingBalance}")
                .Concat(actuals.Select(a => $"actual {a.BudgetId} {a.Month} {a.Amount}"))
                .Concat(overrides.Select(o => $"override {o.LineId} {o.Month} {o.ActualAmount?.ToString() ?? "skipped"}"))
                .Concat(budgets.Select(b => $"budget {b.Id} {b.Name} {b.PlannedAmount}")));
    }
}
