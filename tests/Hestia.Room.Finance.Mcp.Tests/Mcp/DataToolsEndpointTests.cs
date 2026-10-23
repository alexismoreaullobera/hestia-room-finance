using System.Text.Json.Nodes;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Demo;
using Hestia.Room.Finance.Mcp.Tools.Outputs;

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

    public static TheoryData<string> DataTools => ["get_month_summary", "get_cash_flow_forecast", "list_recurring_lines", "list_subscriptions"];

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Theory]
    [MemberData(nameof(DataTools))]
    public async Task ToolsList_AnnouncesADataToolAsReadOnly_WithAMonthArgumentAndAnOutputSchema(string name)
    {
        var tool = await ToolAsync(name);

        Assert.True((bool?)tool["annotations"]?["readOnlyHint"]);
        Assert.True((bool?)tool["annotations"]?["idempotentHint"]);
        Assert.False((bool?)tool["annotations"]?["openWorldHint"]);
        Assert.NotNull(tool["inputSchema"]?["properties"]?["month"]);
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
        var content = result["structuredContent"]!.AsObject();
        var properties = schema["properties"]!.AsObject().Select(p => p.Key).ToHashSet();
        Assert.All(content, property => Assert.Contains(property.Key, properties));
        Assert.All(schema["required"]?.AsArray() ?? [], required => Assert.True(content.ContainsKey((string)required!)));
    }

    [Fact]
    public async Task CashFlowForecast_WithoutMonth_IsTheForecastOfTheCurrentMonth_OnTheSeededDemo()
    {
        var expected = CashFlowForecastOutput.From(DemoMonth(Current).Forecast());

        var content = (await _mcp.CallToolAsync("get_cash_flow_forecast"))["structuredContent"]!;

        Assert.Equal(Current.ToString(), (string?)content["month"]);
        Assert.Equal(expected.StartingBalance, (decimal?)content["startingBalance"]);
        Assert.Equal(expected.LowPoint.Balance, (decimal?)content["lowPoint"]?["balance"]);
        Assert.Equal(expected.LowPoint.Date.ToString("yyyy-MM-dd"), (string?)content["lowPoint"]?["date"]);
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
    }

    [Fact]
    public async Task Lists_GiveTheLinesAndTheSubscriptionsOfTheDemo()
    {
        var lines = (await _mcp.CallToolAsync("list_recurring_lines"))["structuredContent"]!["lines"]!.AsArray();
        var subscriptions = (await _mcp.CallToolAsync("list_subscriptions"))["structuredContent"]!;

        Assert.Equal(Demo.Lines.Count(l => l.IsActiveIn(Current)), lines.Count);
        Assert.Equal(Demo.Lines.Count(l => l.IsSubscription && l.IsActiveIn(Current)), subscriptions["subscriptions"]!.AsArray().Count);
        Assert.Equal(SubscriptionsOutput.From(DemoMonth(Current)).YearlyTotal, (decimal?)subscriptions["yearlyTotal"]);
    }

    [Fact]
    public async Task ToolsCall_WithAnInvalidMonth_IsAToolErrorTheAgentCanRead()
    {
        // A tool error (isError), not a JSON-RPC error: the agent reads the message and can call again.
        var result = await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = "2026-13" });

        Assert.True((bool?)result["isError"]);
        Assert.Contains("YYYY-MM", Text(result));
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
