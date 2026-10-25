using Hestia.Room.Finance.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// Everything an input tool can write (month settings, budget actuals, line overrides), read straight from the
/// database: compared before and after a refused call, it shows that nothing was written anywhere, a row at 0 € and
/// the demo month included. The tests of a collection run one after the other, so no other write interleaves.
/// </summary>
internal static class WrittenRows
{
    public static async Task<string> SnapshotAsync(McpServerFixture mcp)
    {
        await using var scope = mcp.Server.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<FinanceDbContext>();
        var token = TestContext.Current.CancellationToken;

        var plans = await context.MonthPlans.AsNoTracking().OrderBy(p => p.Month).ToListAsync(token);
        var actuals = await context.BudgetActuals.AsNoTracking().OrderBy(a => a.Month).ThenBy(a => a.BudgetId).ToListAsync(token);
        var overrides = await context.RecurringOverrides.AsNoTracking().OrderBy(o => o.Month).ThenBy(o => o.LineId).ToListAsync(token);

        return string.Join("\n",
            plans.Select(p => $"plan {p.Month} {p.StartingBalance}")
                .Concat(actuals.Select(a => $"actual {a.BudgetId} {a.Month} {a.Amount}"))
                .Concat(overrides.Select(o => $"override {o.LineId} {o.Month} {o.ActualAmount?.ToString() ?? "skipped"}")));
    }
}
