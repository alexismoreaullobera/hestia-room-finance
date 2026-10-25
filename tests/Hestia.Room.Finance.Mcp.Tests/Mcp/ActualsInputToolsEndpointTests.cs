using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// The input tools of the actuals end to end on /mcp: set_budget_actual and set_line_actual. They write to the database
/// shared by the /mcp tests: every test uses its own month of 2032, years after the demo plan, which no other test reads.
/// The budgets and lines are the ones of the demo, still running then.
/// </summary>
[Collection(McpServerCollection.Name)]
public class ActualsInputToolsEndpointTests(McpServerFixture mcp)
{
    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    public static TheoryData<string> Tools => ["set_budget_actual", "set_line_actual"];

    [Theory]
    [MemberData(nameof(Tools))]
    public async Task ToolsList_OffersTheToolToTheViewsOnly_AsAnIdempotentWrite_ReturningTheMonthSummary(string name)
    {
        var tool = await ToolAsync(name);
        var summary = await ToolAsync("get_month_summary");

        Assert.Equal(["app"], tool["_meta"]?["ui"]?["visibility"]?.AsArray().Select(v => (string?)v) ?? []);
        Assert.Null(tool["_meta"]?["ui"]?["resourceUri"]);
        Assert.False((bool?)tool["annotations"]?["readOnlyHint"]);
        Assert.True((bool?)tool["annotations"]?["destructiveHint"]);
        Assert.True((bool?)tool["annotations"]?["idempotentHint"]);
        Assert.False((bool?)tool["annotations"]?["openWorldHint"]);
        Assert.True(JsonNode.DeepEquals(summary["outputSchema"], tool["outputSchema"]), $"{name}: output schema differs from get_month_summary.");
    }

    [Fact]
    public async Task ToolsList_SetBudgetActual_RequiresTheMonthTheBudgetAndAnAmountOrNull()
    {
        var schema = (await ToolAsync("set_budget_actual"))["inputSchema"]!;

        Assert.Equal(["month", "budgetId", "actualAmount"], schema["properties"]!.AsObject().Select(p => p.Key));
        // Required, null included: going back to the plan is said explicitly, never by leaving the amount out.
        Assert.Equal(["actualAmount", "budgetId", "month"], schema["required"]!.AsArray().Select(r => (string?)r).Order());
    }

    [Fact]
    public async Task ToolsList_SetLineActual_RequiresTheMonthTheLineAndAStatusOfThreeValues()
    {
        var schema = (await ToolAsync("set_line_actual"))["inputSchema"]!;

        Assert.Equal(["month", "lineId", "status", "actualAmount"], schema["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["lineId", "month", "status"], schema["required"]!.AsArray().Select(r => (string?)r).Order());
        // As text: a client reads the three values in the schema, never a number.
        Assert.Equal(["AsPlanned", "Corrected", "Skipped"], schema["properties"]!["status"]!["enum"]!.AsArray().Select(v => (string?)v));
    }

    [Fact]
    public async Task SetBudgetActual_SavesTheTotal_ThenGoesBackToThePlan()
    {
        const string month = "2032-01";
        var budget = (await SummaryAsync(month))["budgets"]![0]!;
        var id = (string)budget["id"]!;

        var saved = await CallAsync("set_budget_actual", month, new JsonObject { ["budgetId"] = id, ["actualAmount"] = 347.85m });

        // Exactly what get_month_summary gives afterwards: the figures read back from the database.
        Assert.True(JsonNode.DeepEquals(await _mcp.CallToolAsync("get_month_summary", Month(month)), saved), "set_budget_actual differs from get_month_summary.");
        Assert.Equal((347.85m, true), BudgetFigures(saved, id));

        var cleared = await CallAsync("set_budget_actual", month, new JsonObject { ["budgetId"] = id, ["actualAmount"] = null });

        Assert.Equal(((decimal)budget["planned"]!, false), BudgetFigures(cleared, id));
    }

    [Fact]
    public async Task SetBudgetActual_ToZero_IsAnActual()
    {
        const string month = "2032-02";
        var id = (string)(await SummaryAsync(month))["budgets"]![0]!["id"]!;

        var saved = await CallAsync("set_budget_actual", month, new JsonObject { ["budgetId"] = id, ["actualAmount"] = 0 });

        Assert.Equal((0m, true), BudgetFigures(saved, id));
    }

    [Fact]
    public async Task SetLineActual_CorrectsSkipsAndGoesBackToThePlan()
    {
        const string month = "2032-03";
        var line = (await SummaryAsync(month))["lines"]![0]!;
        var id = (string)line["id"]!;

        var corrected = await CallAsync("set_line_actual", month, new JsonObject { ["lineId"] = id, ["status"] = "Corrected", ["actualAmount"] = 92.5m });
        Assert.Equal((92.5m, "Corrected"), LineFigures(corrected, id));
        Assert.True(JsonNode.DeepEquals(await _mcp.CallToolAsync("get_month_summary", Month(month)), corrected), "set_line_actual differs from get_month_summary.");

        var skipped = await CallAsync("set_line_actual", month, new JsonObject { ["lineId"] = id, ["status"] = "Skipped" });
        Assert.Equal((0m, "Skipped"), LineFigures(skipped, id));

        var asPlanned = await CallAsync("set_line_actual", month, new JsonObject { ["lineId"] = id, ["status"] = "AsPlanned" });
        Assert.Equal(((decimal)line["planned"]!, "AsPlanned"), LineFigures(asPlanned, id));
    }

    [Fact]
    public async Task ClearingTwice_IsNoError()
    {
        // Idempotent: the same "back to the plan" sent twice (a host retrying) changes nothing the second time.
        const string month = "2032-04";
        var summary = await SummaryAsync(month);
        var budgetId = (string)summary["budgets"]![0]!["id"]!;
        var lineId = (string)summary["lines"]![0]!["id"]!;

        for (var i = 0; i < 2; i++)
        {
            Assert.NotEqual(true, (bool?)(await _mcp.CallToolAsync("set_budget_actual", Arguments(month, new JsonObject { ["budgetId"] = budgetId, ["actualAmount"] = null })))["isError"]);
            Assert.NotEqual(true, (bool?)(await _mcp.CallToolAsync("set_line_actual", Arguments(month, new JsonObject { ["lineId"] = lineId, ["status"] = "AsPlanned" })))["isError"]);
        }
    }

    [Fact]
    public async Task SetLineActual_OnAQuarterlyLineDueThisMonth_IsAccepted()
    {
        // The car insurance of the demo is quarterly, due in March: a line of the month like a monthly one.
        const string month = "2032-03";
        var insurance = (await SummaryAsync(month))["lines"]!.AsArray().Single(l => (string?)l!["name"] == "Assurance auto")!;

        var corrected = await CallAsync("set_line_actual", month, new JsonObject { ["lineId"] = (string)insurance["id"]!, ["status"] = "Corrected", ["actualAmount"] = 170m });

        Assert.Equal((170m, "Corrected"), LineFigures(corrected, (string)insurance["id"]!));
    }

    public static TheoryData<string, string, string> InvalidCalls => new()
    {
        // Tool, arguments with the placeholders BUDGET, LINE (due) and NOT_DUE (running, not due), expected words.
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "BUDGET", "actualAmount": -1 }""", "0 or more" },
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "BUDGET", "actualAmount": 12.345 }""", "two decimals" },
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "BUDGET", "actualAmount": 1000000000 }""", "999999999.99" },
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "LINE", "actualAmount": 10 }""", "No budget with this identifier in 2032-05." },
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "LINE", "actualAmount": null }""", "No budget with this identifier in 2032-05." },
        { "set_budget_actual", """{ "month": "2032-13", "budgetId": "BUDGET", "actualAmount": 10 }""", "YYYY-MM" },
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "not-a-guid", "actualAmount": 10 }""", "An error occurred invoking 'set_budget_actual'." },
        { "set_budget_actual", """{ "month": "2032-05", "budgetId": "BUDGET" }""", "An error occurred invoking 'set_budget_actual'." },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "LINE", "status": "Corrected", "actualAmount": 0 }""", "more than 0" },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "LINE", "status": "Corrected" }""", "needs its actualAmount" },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "LINE", "status": "Skipped", "actualAmount": 10 }""", "for a Corrected line only" },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "LINE", "status": "AsPlanned", "actualAmount": 10 }""", "for a Corrected line only" },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "NOT_DUE", "status": "Skipped" }""", "No recurring line with this identifier is due in 2032-05." },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "NOT_DUE", "status": "Corrected", "actualAmount": 10 }""", "No recurring line with this identifier is due in 2032-05." },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "NOT_DUE", "status": "AsPlanned" }""", "No recurring line with this identifier is due in 2032-05." },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "BUDGET", "status": "AsPlanned" }""", "No recurring line with this identifier is due in 2032-05." },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "LINE", "status": 7 }""", "Unknown status" },
        { "set_line_actual", """{ "month": "2032-05", "lineId": "LINE", "status": "Paid" }""", "An error occurred invoking 'set_line_actual'." },
    };

    [Theory]
    [MemberData(nameof(InvalidCalls))]
    public async Task InvalidCalls_AreToolErrors_AndWriteNothing(string tool, string arguments, string expectedMessage)
    {
        var summary = await SummaryAsync("2032-05");
        var lines = (await _mcp.CallToolAsync("list_recurring_lines", Month("2032-05")))["structuredContent"]!["lines"]!.AsArray();
        var notDue = lines.First(l => l!["dueDate"] is null)!;
        var budgetId = (string)summary["budgets"]![0]!["id"]!;
        var lineId = (string)summary["lines"]![0]!["id"]!;
        var json = arguments
            .Replace("NOT_DUE", (string)notDue["id"]!)
            .Replace("BUDGET", budgetId)
            .Replace("LINE", lineId);
        // Something to lose: a refused call that would clear before refusing shows in the snapshot too.
        await CallAsync("set_budget_actual", "2032-05", new JsonObject { ["budgetId"] = budgetId, ["actualAmount"] = 50 });
        await CallAsync("set_line_actual", "2032-05", new JsonObject { ["lineId"] = lineId, ["status"] = "Skipped" });
        var before = await WrittenRows.SnapshotAsync(mcp);

        var result = await _mcp.CallToolAsync(tool, JsonNode.Parse(json)!.AsObject());

        Assert.True((bool?)result["isError"]);
        Assert.Contains(expectedMessage, (string)result["content"]!.AsArray().Single()!["text"]!);
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    private async Task<JsonNode> SummaryAsync(string month) =>
        (await _mcp.CallToolAsync("get_month_summary", Month(month)))["structuredContent"]!;

    private async Task<JsonNode> CallAsync(string tool, string month, JsonObject arguments)
    {
        var result = await _mcp.CallToolAsync(tool, Arguments(month, arguments));
        Assert.NotEqual(true, (bool?)result["isError"]);
        return result;
    }

    private static JsonObject Arguments(string month, JsonObject arguments)
    {
        arguments["month"] = month;
        return arguments;
    }

    private static JsonObject Month(string month) => new() { ["month"] = month };

    private static (decimal Actual, bool Entered) BudgetFigures(JsonNode result, string id)
    {
        var budget = result["structuredContent"]!["budgets"]!.AsArray().Single(b => (string?)b!["id"] == id)!;
        return ((decimal)budget["actual"]!, (bool)budget["entered"]!);
    }

    private static (decimal Actual, string Status) LineFigures(JsonNode result, string id)
    {
        var line = result["structuredContent"]!["lines"]!.AsArray().Single(l => (string?)l!["id"] == id)!;
        return ((decimal)line["actual"]!, (string)line["status"]!);
    }

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;
}
