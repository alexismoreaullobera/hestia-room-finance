using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// The input tools of the budgets end to end on /mcp: create_budget, update_budget, delete_budget. Budgets belong to the
/// whole plan, shared by the /mcp tests: every test creates its own budgets (named with a test prefix) and they are
/// deleted after each test, the budgets of the demo never touched.
/// </summary>
[Collection(McpServerCollection.Name)]
public class BudgetInputToolsEndpointTests(McpServerFixture mcp) : IAsyncLifetime
{
    private const string Month = "2031-10";

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>After each test, its budgets and months cleared: the other classes of the collection never see them.</summary>
    public async ValueTask DisposeAsync() => await WrittenRows.DeleteTestWritesAsync(mcp);

    public static TheoryData<string> Tools => ["create_budget", "update_budget", "delete_budget"];

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task ToolsList_OffersTheToolToTheViewsOnly_ReturningThePlan(string name)
    {
        var tool = await ToolAsync(name);
        var plan = await ToolAsync("get_plan");

        Assert.Equal(["app"], tool["_meta"]?["ui"]?["visibility"]?.AsArray().Select(v => (string?)v) ?? []);
        Assert.Null(tool["_meta"]?["ui"]?["resourceUri"]);
        Assert.False((bool?)tool["annotations"]?["readOnlyHint"]);
        Assert.False((bool?)tool["annotations"]?["openWorldHint"]);
        Assert.True(JsonNode.DeepEquals(plan["outputSchema"], tool["outputSchema"]), $"{name}: output schema differs from get_plan.");
    }

    [Theory]
    // Creating twice makes two attempts (the second refused): not idempotent, adds without destroying.
    [InlineData("create_budget", false, false)]
    [InlineData("update_budget", true, true)]
    [InlineData("delete_budget", true, true)]
    public async Task ToolsList_DeclaresWhatEachWriteDoes(string name, bool destructive, bool idempotent)
    {
        var hints = (await ToolAsync(name))["annotations"]!;

        Assert.Equal(destructive, (bool?)hints["destructiveHint"]);
        Assert.Equal(idempotent, (bool?)hints["idempotentHint"]);
    }

    [Theory]
    [InlineData("create_budget", new[] { "month", "name", "plannedAmount" })]
    [InlineData("update_budget", new[] { "month", "budgetId", "name", "plannedAmount" })]
    [InlineData("delete_budget", new[] { "month", "budgetId" })]
    public async Task ToolsList_RequiresEveryArgument(string name, string[] arguments)
    {
        var schema = (await ToolAsync(name))["inputSchema"]!;

        Assert.Equal(arguments, schema["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal(arguments.Order(), schema["required"]!.AsArray().Select(r => (string?)r).Order());
    }

    [Fact]
    public async Task CreateBudget_AddsItToThePlan_ItsNameTrimmed_AndToEveryMonth()
    {
        var created = await CallAsync("create_budget", new JsonObject { ["name"] = $"  {WrittenRows.TestBudgetPrefix}Cadeaux ", ["plannedAmount"] = 40.5m });

        // Exactly what get_plan gives afterwards: read back from the database.
        Assert.True(JsonNode.DeepEquals(await _mcp.CallToolAsync("get_plan", MonthOnly()), created), "create_budget differs from get_plan.");
        var budget = Budget(created, $"{WrittenRows.TestBudgetPrefix}Cadeaux");
        Assert.Equal((40.5m, 0), ((decimal)budget["plannedAmount"]!, (int)budget["actualsEntered"]!));
        // A budget of the plan: in the month summary of any month, past ones included.
        var summary = (await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = "2030-01" }))["structuredContent"]!;
        Assert.Contains(summary["budgets"]!.AsArray(), b => (string?)b!["id"] == (string?)budget["id"]);
    }

    [Fact]
    public async Task UpdateBudget_RenamesIt_AndChangesItsAmount()
    {
        var id = await CreateAsync("Cadeaux", 40m);

        var updated = await CallAsync("update_budget", new JsonObject { ["budgetId"] = id, ["name"] = $"{WrittenRows.TestBudgetPrefix}Anniversaires", ["plannedAmount"] = 55m });

        var budget = Budget(updated, $"{WrittenRows.TestBudgetPrefix}Anniversaires");
        Assert.Equal((id, 55m), ((string)budget["id"]!, (decimal)budget["plannedAmount"]!));
        Assert.DoesNotContain(updated["structuredContent"]!["budgets"]!.AsArray(), b => (string?)b!["name"] == $"{WrittenRows.TestBudgetPrefix}Cadeaux");
    }

    [Fact]
    public async Task UpdateBudget_KeepsItsOwnName_WithAnotherAmount_OrAnotherCase()
    {
        var id = await CreateAsync("Cadeaux", 40m);

        await CallAsync("update_budget", new JsonObject { ["budgetId"] = id, ["name"] = $"{WrittenRows.TestBudgetPrefix}Cadeaux", ["plannedAmount"] = 45m });
        var recased = await CallAsync("update_budget", new JsonObject { ["budgetId"] = id, ["name"] = $"{WrittenRows.TestBudgetPrefix}CADEAUX", ["plannedAmount"] = 45m });

        Assert.Equal(45m, (decimal)Budget(recased, $"{WrittenRows.TestBudgetPrefix}CADEAUX")["plannedAmount"]!);
    }

    [Fact]
    public async Task DeleteBudget_RemovesIt_WithItsActuals()
    {
        var id = await CreateAsync("Cadeaux", 40m);
        await CallAsync("set_budget_actual", new JsonObject { ["budgetId"] = id, ["actualAmount"] = 12m }, month: "2031-11");
        Assert.Equal(1, (int)Budget(await _mcp.CallToolAsync("get_plan", MonthOnly()), $"{WrittenRows.TestBudgetPrefix}Cadeaux")["actualsEntered"]!);

        var deleted = await CallAsync("delete_budget", new JsonObject { ["budgetId"] = id });

        Assert.DoesNotContain(deleted["structuredContent"]!["budgets"]!.AsArray(), b => (string?)b!["id"] == id);
        var summary = (await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = "2031-11" }))["structuredContent"]!;
        Assert.DoesNotContain(summary["budgets"]!.AsArray(), b => (string?)b!["id"] == id);
    }

    public static TheoryData<string, string, string> InvalidCalls => new()
    {
        // Tool, arguments with the placeholders MINE (a budget created by the test) and DEMO (the name of a demo budget).
        { "create_budget", """{ "month": "2031-10", "name": "DEMO", "plannedAmount": 10 }""", "Another budget already has this name." },
        { "create_budget", """{ "month": "2031-10", "name": "  ", "plannedAmount": 10 }""", "Invalid budget" },
        { "create_budget", """{ "month": "2031-10", "name": "Test budget LONG", "plannedAmount": 10 }""", "Invalid budget" },
        { "create_budget", """{ "month": "2031-10", "name": "Test budget a\nb", "plannedAmount": 10 }""", "Invalid budget" },
        { "create_budget", """{ "month": "2031-10", "name": "Test budget x", "plannedAmount": 0 }""", "Invalid budget" },
        { "create_budget", """{ "month": "2031-10", "name": "Test budget x", "plannedAmount": -5 }""", "Invalid budget" },
        { "create_budget", """{ "month": "2031-10", "name": "Test budget x", "plannedAmount": 12.345 }""", "Invalid budget" },
        { "create_budget", """{ "month": "2031-10", "name": "Test budget x", "plannedAmount": 1000000000 }""", "999999999.99" },
        { "create_budget", """{ "month": "2031-13", "name": "Test budget x", "plannedAmount": 10 }""", "YYYY-MM" },
        { "create_budget", """{ "month": "2031-10", "plannedAmount": 10 }""", "An error occurred invoking 'create_budget'." },
        { "update_budget", """{ "month": "2031-10", "budgetId": "MINE", "name": "DEMO", "plannedAmount": 10 }""", "Another budget already has this name." },
        { "update_budget", """{ "month": "2031-10", "budgetId": "MINE", "name": "Test budget x", "plannedAmount": 0 }""", "Invalid budget" },
        { "update_budget", """{ "month": "2031-10", "budgetId": "00000000-0000-0000-0000-000000000099", "name": "Test budget x", "plannedAmount": 10 }""", "No budget with this identifier in 2031-10." },
        { "delete_budget", """{ "month": "2031-10", "budgetId": "00000000-0000-0000-0000-000000000099" }""", "No budget with this identifier in 2031-10." },
        { "delete_budget", """{ "month": "2031-10", "budgetId": "not-a-guid" }""", "An error occurred invoking 'delete_budget'." },
    };

    [Theory]
    [MemberData(nameof(InvalidCalls))]
    public async Task InvalidCalls_AreToolErrors_AndWriteNothing(string tool, string arguments, string expectedMessage)
    {
        var mine = await CreateAsync("Cadeaux", 40m);
        var demo = (string)(await _mcp.CallToolAsync("get_plan", MonthOnly()))["structuredContent"]!["budgets"]!.AsArray()
            .First(b => !((string)b!["name"]!).StartsWith(WrittenRows.TestBudgetPrefix, StringComparison.Ordinal))!["name"]!;
        // The demo name in lower case: two names differing only by case are the same name.
        var json = arguments
            .Replace("MINE", mine)
            .Replace("\"DEMO\"", $"\"{demo.ToLowerInvariant()}\"")
            .Replace("Test budget LONG", WrittenRows.TestBudgetPrefix + new string('x', 101));
        var before = await WrittenRows.SnapshotAsync(mcp);

        var result = await _mcp.CallToolAsync(tool, JsonNode.Parse(json)!.AsObject());

        Assert.True((bool?)result["isError"]);
        Assert.Contains(expectedMessage, (string)result["content"]!.AsArray().Single()!["text"]!);
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    /// <summary>Creates a budget named with the test prefix; returns its identifier.</summary>
    private async Task<string> CreateAsync(string name, decimal plannedAmount)
    {
        var created = await CallAsync("create_budget", new JsonObject { ["name"] = WrittenRows.TestBudgetPrefix + name, ["plannedAmount"] = plannedAmount });
        return (string)Budget(created, WrittenRows.TestBudgetPrefix + name)["id"]!;
    }

    private async Task<JsonNode> CallAsync(string tool, JsonObject arguments, string month = Month)
    {
        arguments["month"] = month;
        var result = await _mcp.CallToolAsync(tool, arguments);
        Assert.NotEqual(true, (bool?)result["isError"]);
        return result;
    }

    private static JsonObject MonthOnly() => new() { ["month"] = Month };

    private static JsonNode Budget(JsonNode result, string name) =>
        result["structuredContent"]!["budgets"]!.AsArray().Single(b => (string?)b!["name"] == name)!;

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;
}
