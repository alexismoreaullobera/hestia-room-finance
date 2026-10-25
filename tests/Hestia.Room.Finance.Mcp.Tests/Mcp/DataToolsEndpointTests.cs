using System.Globalization;
using System.Text.Json.Nodes;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure;
using Hestia.Room.Finance.Infrastructure.Demo;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// The four data tools end to end: real HTTP pipeline, real SQL Server (Testcontainers), demo plan seeded by the
/// development startup. The expected figures come from the domain applied to the same demo plan, never written by hand.
/// </summary>
[Collection(McpServerCollection.Name)]
public class DataToolsEndpointTests(McpServerFixture mcp)
{
    private static readonly YearMonth Current = DevelopmentMcpServerFactory.CurrentMonth;
    private static readonly YearMonth Previous = Current.AddMonths(-1);
    private static readonly DemoPlan Demo = DemoPlan.For(Current);

    public static TheoryData<string> DataTools => ["get_month_summary", "get_cash_flow_forecast", "list_recurring_lines", "list_subscriptions", "get_plan"];

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Fact]
    public void TheServer_UsesTheTestDatabase_NeverTheLocalOne()
    {
        using var scope = mcp.Server.Services.CreateScope();

        var connectionString = scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Database.GetConnectionString();

        Assert.StartsWith("finance_test_", new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString).InitialCatalog);
    }

    [Theory]
    [MemberData(nameof(DataTools))]
    public async Task ToolsList_AnnouncesADataToolAsReadOnly_WithAMonthArgumentAndAnOutputSchema(string name)
    {
        var tool = await ToolAsync(name);

        Assert.True((bool?)tool["annotations"]?["readOnlyHint"]);
        Assert.True((bool?)tool["annotations"]?["idempotentHint"]);
        Assert.False((bool?)tool["annotations"]?["openWorldHint"]);
        // The only argument asked of the model: the reader, the clock and the cancellation token stay out of it.
        Assert.Equal(["month"], tool["inputSchema"]!["properties"]!.AsObject().Select(p => p.Key));
        // Optional: the agent may leave it out to read the current month.
        Assert.DoesNotContain("month", tool["inputSchema"]?["required"]?.AsArray().Select(r => (string?)r) ?? []);
        Assert.Equal("object", (string?)tool["outputSchema"]?["type"]);
    }

    [Theory]
    [MemberData(nameof(DataTools))]
    public async Task ToolsCall_ReturnsATextSummaryAndStructuredContentMatchingTheSchema(string name)
    {
        var schema = (await ToolAsync(name))["outputSchema"]!;

        var result = await _mcp.CallToolAsync(name);

        Assert.False((bool?)result["isError"] ?? false);
        Assert.Contains("mars 2027", Text(result));
        // At every level: a property dropped deep in a list (a null date) breaks a client validating the result.
        SchemaAssert.Matches(schema, result["structuredContent"]!);
    }

    [Fact]
    public async Task CashFlowForecast_WithoutMonth_IsTheForecastOfTheCurrentMonth_OnTheSeededDemo()
    {
        var expected = CashFlowForecastOutput.From(DemoMonth(Current).Forecast());

        var content = (await _mcp.CallToolAsync("get_cash_flow_forecast"))["structuredContent"]!;

        Assert.Equal(Current.ToString(), (string?)content["month"]);
        Assert.Equal(expected.StartingBalance, (decimal?)content["startingBalance"]);
        Assert.Equal(expected.LowPoint.Balance, (decimal?)content["lowPoint"]?["balance"]);
        Assert.Equal(expected.LowPoint.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), (string?)content["lowPoint"]?["date"]);
        Assert.Equal(expected.CashBuffer, (decimal?)content["cashBuffer"]);
        Assert.True((bool?)content["hasAlert"], "The demo is meant to show the alert of the current month.");
        Assert.Equal(Current.DaysInMonth, content["days"]!.AsArray().Count);
    }

    [Fact]
    public async Task MonthSummary_ForAPastMonth_TakesItsActualsIntoAccount()
    {
        var expected = MonthSummaryOutput.From(DemoMonth(Previous));

        var content = (await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = Previous.ToString() }))["structuredContent"]!;

        Assert.Equal(expected.Planned.Budgets, (decimal?)content["planned"]?["budgets"]);
        Assert.Equal(expected.Actual.Budgets, (decimal?)content["actual"]?["budgets"]);
        Assert.Equal(expected.Actual.RecurringExpenses, (decimal?)content["actual"]?["recurringExpenses"]);
        Assert.NotEqual(expected.Planned.Budgets, expected.Actual.Budgets);
        // The detail behind the totals: each budget with its actual, each line due with what happened.
        Assert.Equal(
            expected.Budgets.Select(b => (b.Name, b.Actual, b.Entered)),
            content["budgets"]!.AsArray().Select(b => ((string)b!["name"]!, (decimal)b["actual"]!, (bool)b["entered"]!)));
        Assert.Contains(expected.Budgets, b => b.Entered);
        Assert.Equal(
            expected.Lines.Select(l => (l.Name, l.Actual, l.Status.ToString())),
            content["lines"]!.AsArray().Select(l => ((string)l!["name"]!, (decimal)l["actual"]!, (string)l["status"]!)));
    }

    [Fact]
    public async Task GetPlan_GivesTheBudgetsOfTheDemo_WithTheMonthsTheirActualsWereEnteredFor()
    {
        var budgets = (await _mcp.CallToolAsync("get_plan"))["structuredContent"]!["budgets"]!.AsArray();

        // By name, compared by name (the demo built here has identifiers of its own): the demo enters an actual for
        // every budget on each of its three past months.
        var expected = Demo.Budgets
            .OrderBy(b => b.Name, StringComparer.Create(CultureInfo.GetCultureInfo("fr-FR"), ignoreCase: true))
            .Select(b => (b.Name, b.PlannedAmount, Demo.BudgetActuals.Count(a => a.BudgetId == b.Id)));
        Assert.Equal(expected, budgets.Select(b => ((string)b!["name"]!, (decimal)b["plannedAmount"]!, (int)b["actualsEntered"]!)));
    }

    [Fact]
    public async Task ListRecurringLines_GivesTheLinesOfTheDemo_InOrder()
    {
        var lines = (await _mcp.CallToolAsync("list_recurring_lines"))["structuredContent"]!["lines"]!.AsArray();

        // By name: the identifiers differ from one DemoPlan.For call to another.
        Assert.Equal(RecurringLinesOutput.From(DemoMonth(Current)).Lines.Select(l => l.Name), lines.Select(l => (string)l!["name"]!));
    }

    [Fact]
    public async Task ListSubscriptions_GivesTheSubscriptionsOfTheDemo_InOrder_WithTheirTotals()
    {
        var expected = SubscriptionsOutput.From(DemoMonth(Current));

        var content = (await _mcp.CallToolAsync("list_subscriptions"))["structuredContent"]!;

        Assert.Equal(expected.Subscriptions.Select(s => s.Name), content["subscriptions"]!.AsArray().Select(s => (string)s!["name"]!));
        Assert.Equal(expected.MonthlyTotal, (decimal?)content["monthlyTotal"]);
        Assert.Equal(expected.YearlyTotal, (decimal?)content["yearlyTotal"]);
    }

    [Fact]
    public async Task ToolsCall_WithAnInvalidMonth_IsAToolErrorTheAgentCanRead()
    {
        // A tool error (isError), not a JSON-RPC error: the agent reads the message and can call again.
        var result = await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = "2026-13" });

        Assert.True((bool?)result["isError"]);
        Assert.Contains("YYYY-MM", Text(result));
    }

    [Fact]
    public async Task ToolsCall_WithAMonthOfTheWrongType_IsAToolErrorWithAGenericMessage()
    {
        // Not a McpException: the SDK hides the details (no stack, no internal message) behind a generic message.
        var result = await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = 202704 });

        Assert.True((bool?)result["isError"]);
        Assert.Equal("An error occurred invoking 'get_month_summary'.", Text(result));
    }

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;

    private static string Text(JsonNode result) => (string)result["content"]!.AsArray().Single()!["text"]!;

    /// <summary>The demo month as the reader loads it: the plan, and the actuals and overrides of that month only.</summary>
    private static FinanceMonth DemoMonth(YearMonth month) => new(
        Demo.MonthPlans.SingleOrDefault(p => p.Month == month) ?? new MonthPlan(month),
        Demo.Lines.Where(l => l.IsActiveIn(month)),
        Demo.Budgets,
        Demo.BudgetActuals.Where(a => a.Month == month),
        Demo.Overrides.Where(o => o.Month == month));
}
