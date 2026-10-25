using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// The input tools end to end on /mcp. They write to the database shared by the /mcp tests: every test uses its own
/// month, years after the demo plan, so the reads of the other tests and of the other cases never see these writes.
/// </summary>
[Collection(McpServerCollection.Name)]
public class InputToolsEndpointTests(McpServerFixture mcp) : IAsyncLifetime
{
    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>After each test, its months cleared: the other classes of the collection never see these writes.</summary>
    public async ValueTask DisposeAsync() => await WrittenRows.DeleteTestMonthsAsync(mcp);

    [Fact]
    public async Task ToolsList_OffersSetStartingBalanceToTheViewsOnly_WithoutOpeningAView()
    {
        var tool = await ToolAsync("set_starting_balance");

        // Hidden from the model by the hosts (the agent has no write access in the v1), callable by the views.
        Assert.Equal(["app"], tool["_meta"]?["ui"]?["visibility"]?.AsArray().Select(v => (string?)v) ?? []);
        // Called from a view already shown: no resource URI, so the host opens no other view.
        Assert.Null(tool["_meta"]?["ui"]?["resourceUri"]);
        Assert.Equal("Saisie : solde de départ du mois", (string?)tool["annotations"]?["title"] ?? (string?)tool["title"]);
    }

    [Fact]
    public async Task ToolsList_SetStartingBalance_IsAnIdempotentWriteOfTheRoomOnly()
    {
        var hints = (await ToolAsync("set_starting_balance"))["annotations"]!;

        Assert.False((bool?)hints["readOnlyHint"]);
        // Destructive: the balance saved before is replaced. Idempotent: the same call twice gives the same state.
        Assert.True((bool?)hints["destructiveHint"]);
        Assert.True((bool?)hints["idempotentHint"]);
        Assert.False((bool?)hints["openWorldHint"]);
    }

    [Fact]
    public async Task ToolsList_SetStartingBalance_RequiresAMonthAndABalance_AndReturnsTheForecastSchema()
    {
        var tool = await ToolAsync("set_starting_balance");
        var forecast = await ToolAsync("get_cash_flow_forecast");

        // Only the two arguments: the writer, the reader, the clock and the cancellation token stay out of the schema.
        Assert.Equal(["month", "startingBalance"], tool["inputSchema"]!["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal(["month", "startingBalance"], tool["inputSchema"]!["required"]!.AsArray().Select(r => (string?)r).Order());
        Assert.True(JsonNode.DeepEquals(forecast["outputSchema"], tool["outputSchema"]), "The output schema differs from get_cash_flow_forecast.");
    }

    [Fact]
    public async Task ToolsCall_SavesTheBalance_AndReturnsExactlyTheForecastOfTheDataTool()
    {
        var result = await SetAsync("2031-01", 1234.56m);

        Assert.NotEqual(true, (bool?)result["isError"]);
        Assert.Equal(1234.56m, (decimal?)result["structuredContent"]!["startingBalance"]);
        // The data tool, called afterwards, reads the saved value: same text, same structured content.
        var forecast = await _mcp.CallToolAsync("get_cash_flow_forecast", new JsonObject { ["month"] = "2031-01" });
        Assert.True(JsonNode.DeepEquals(forecast, result), "set_starting_balance differs from get_cash_flow_forecast.");
    }

    [Fact]
    public async Task ToolsCall_Again_ReplacesTheBalance_AndLeavesTheOtherMonthsUntouched()
    {
        await SetAsync("2031-03", 500m);
        await SetAsync("2031-04", 80m);

        await SetAsync("2031-03", -42.5m);

        Assert.Equal(-42.5m, await StartingBalanceAsync("2031-03"));
        Assert.Equal(80m, await StartingBalanceAsync("2031-04"));
    }

    public static TheoryData<string, string> InvalidCalls => new()
    {
        // Arguments as raw JSON, as any client may send them; the expected words of the message.
        { """{ "month": "2031-13", "startingBalance": 10 }""", "YYYY-MM" },
        { """{ "month": "", "startingBalance": 10 }""", "YYYY-MM" },
        { """{ "month": null, "startingBalance": 10 }""", "YYYY-MM" },
        { """{ "month": "2031-02", "startingBalance": 12.345 }""", "two decimals" },
        { """{ "month": "2031-02", "startingBalance": 1000000000 }""", "999999999.99" },
        { """{ "month": "2031-02", "startingBalance": -1000000000 }""", "999999999.99" },
        // Beyond decimal itself, of the wrong type, missing: refused by the SDK before the tool, with its generic message
        // (the whole message: a crash inside the tool would give the same words with another tool name, never these).
        { """{ "month": "2031-02", "startingBalance": 1e40 }""", "An error occurred invoking 'set_starting_balance'." },
        { """{ "month": "2031-02", "startingBalance": "douze" }""", "An error occurred invoking 'set_starting_balance'." },
        { """{ "month": "2031-02", "startingBalance": "12.345" }""", "two decimals" },
        { """{ "month": "2031-02" }""", "An error occurred invoking 'set_starting_balance'." },
        { """{ "startingBalance": 10 }""", "An error occurred invoking 'set_starting_balance'." },
    };

    [Theory]
    [MemberData(nameof(InvalidCalls))]
    public async Task ToolsCall_WithInvalidArguments_IsAToolError_AndWritesNothing(string arguments, string expectedMessage)
    {
        var before = await WrittenRows.SnapshotAsync(mcp);

        var result = await _mcp.CallToolAsync("set_starting_balance", JsonNode.Parse(arguments)!.AsObject());

        // A tool error (isError), not a JSON-RPC error: the view shows its message and keeps the form open.
        Assert.True((bool?)result["isError"]);
        Assert.Contains(expectedMessage, Text(result));
        // Nothing written anywhere: not 2031-02, and not the current month a wrong fallback would write to (the demo
        // month the other classes of the collection read). The tests of a collection run one after the other.
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    [Fact]
    public async Task ToolsCall_WithTheBalanceAsANumericString_ReadsIt_ThroughTheSameRules()
    {
        // The SDK reads JSON with the web defaults (numbers allowed as strings): accepted, and still checked by the
        // domain like any number (the case "12.345" above is refused).
        var result = await _mcp.CallToolAsync("set_starting_balance", JsonNode.Parse("""{ "month": "2031-05", "startingBalance": "12.50" }""")!.AsObject());

        Assert.NotEqual(true, (bool?)result["isError"]);
        Assert.Equal(12.5m, await StartingBalanceAsync("2031-05"));
    }

    private Task<JsonNode> SetAsync(string month, decimal startingBalance) =>
        _mcp.CallToolAsync("set_starting_balance", new JsonObject { ["month"] = month, ["startingBalance"] = startingBalance });

    private async Task<decimal?> StartingBalanceAsync(string month) =>
        (decimal?)(await _mcp.CallToolAsync("get_cash_flow_forecast", new JsonObject { ["month"] = month }))["structuredContent"]!["startingBalance"];

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;

    private static string Text(JsonNode result) => (string)result["content"]!.AsArray().Single()!["text"]!;
}
