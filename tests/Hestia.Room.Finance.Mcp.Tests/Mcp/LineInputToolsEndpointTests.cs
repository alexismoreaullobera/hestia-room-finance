using System.Text.Json.Nodes;
using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// The input tools of the recurring lines end to end on /mcp: create_line, update_line, end_line, delete_line. Lines
/// belong to the whole plan, shared by the /mcp tests: every test creates its own lines (named with a test prefix) and
/// they are deleted after each test, the lines of the demo never touched.
/// </summary>
[Collection(McpServerCollection.Name)]
public class LineInputToolsEndpointTests(McpServerFixture mcp) : IAsyncLifetime
{
    private const string Month = "2031-10";
    private const string Prefix = WrittenRows.TestLinePrefix;

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    /// <summary>After each test, its lines and months cleared: the other classes of the collection never see them.</summary>
    public async ValueTask DisposeAsync() => await WrittenRows.DeleteTestWritesAsync(mcp);

    public static TheoryData<string> Tools => ["create_line", "update_line", "end_line", "delete_line"];

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
    [InlineData("create_line", false, false)]
    [InlineData("update_line", true, true)]
    [InlineData("end_line", true, true)]
    [InlineData("delete_line", true, true)]
    public async Task ToolsList_DeclaresWhatEachWriteDoes(string name, bool destructive, bool idempotent)
    {
        var hints = (await ToolAsync(name))["annotations"]!;

        Assert.Equal(destructive, (bool?)hints["destructiveHint"]);
        Assert.Equal(idempotent, (bool?)hints["idempotentHint"]);
    }

    private static readonly string[] LineFields =
        ["kind", "name", "amount", "frequency", "dayOfMonth", "anchorMonth", "startDate", "endDate", "category", "isSubscription", "commitmentEndDate"];

    [Theory]
    [InlineData("create_line", new[] { "month" }, true)]
    [InlineData("update_line", new[] { "month", "lineId" }, true)]
    [InlineData("end_line", new[] { "month", "lineId", "endDate" }, false)]
    [InlineData("delete_line", new[] { "month", "lineId", "overridesEntered" }, false)]
    public async Task ToolsList_RequiresEveryArgument_NullableOnesIncluded(string name, string[] arguments, bool withLineFields)
    {
        var schema = (await ToolAsync(name))["inputSchema"]!;
        var expected = withLineFields ? [.. arguments, .. LineFields] : arguments;

        // Every argument required, even those that may be null: a client never leaves one out by mistake.
        Assert.Equal(expected, schema["properties"]!.AsObject().Select(p => p.Key));
        Assert.Equal(expected.Order(), schema["required"]!.AsArray().Select(r => (string?)r).Order());
    }

    [Fact]
    public async Task ToolsList_CategoryDescription_NamesEveryCategory()
    {
        var description = (string)(await ToolAsync("create_line"))["inputSchema"]!["properties"]!["category"]!["description"]!;

        Assert.All(Enum.GetNames<Category>(), category => Assert.Contains(category, description));
    }

    [Fact]
    public async Task CreateLine_AddsItToThePlan_WithEveryField_ItsNameTrimmed()
    {
        var created = await CallAsync("create_line", Line($"  {Prefix}Assurance ", amount: 180m, frequency: "Yearly", day: 15, anchorMonth: 3,
            start: "2031-01-01", end: "2033-12-31", category: "Insurance", subscription: true, commitmentEnd: "2031-12-31"));

        // Exactly what get_plan gives afterwards: read back from the database.
        Assert.True(JsonNode.DeepEquals(await _mcp.CallToolAsync("get_plan", MonthOnly()), created), "create_line differs from get_plan.");
        var line = LineNamed(created, $"{Prefix}Assurance");
        Assert.Equal(
            ("Expense", 180m, "Yearly", 15, 3, "2031-01-01", "2033-12-31", "Insurance", true, "2031-12-31", 0),
            ((string)line["kind"]!, (decimal)line["amount"]!, (string)line["frequency"]!, (int)line["dayOfMonth"]!, (int?)line["anchorMonth"],
                (string)line["startDate"]!, (string?)line["endDate"], (string)line["category"]!, (bool)line["isSubscription"]!,
                (string?)line["commitmentEndDate"], (int)line["overridesEntered"]!));
    }

    [Fact]
    public async Task CreateLine_ShowsInTheMonthSummary_TheForecast_AndTheSubscriptions()
    {
        var before = await ForecastBalancesAsync();

        await CallAsync("create_line", Line($"{Prefix}Streaming", amount: 13.49m, day: 20, subscription: true));

        var summary = (await _mcp.CallToolAsync("get_month_summary", MonthOnly()))["structuredContent"]!;
        var subscriptions = (await _mcp.CallToolAsync("list_subscriptions", MonthOnly()))["structuredContent"]!;
        Assert.Contains(summary["lines"]!.AsArray(), l => (string?)l!["name"] == $"{Prefix}Streaming");
        Assert.Contains(subscriptions["subscriptions"]!.AsArray(), s => (string?)s!["name"] == $"{Prefix}Streaming");
        // The lines of the demo run in 2031 too: the forecast moves by its amount from the 20th on, and only then.
        var after = await ForecastBalancesAsync();
        Assert.Equal(before.Take(19), after.Take(19));
        Assert.Equal(before.Skip(19).Select(balance => balance - 13.49m), after.Skip(19));
    }

    private async Task<decimal[]> ForecastBalancesAsync() =>
        [.. (await _mcp.CallToolAsync("get_cash_flow_forecast", MonthOnly()))["structuredContent"]!["days"]!.AsArray()
            .Select(day => (decimal)day!["balance"]!)];

    [Fact]
    public async Task UpdateLine_ChangesItEverywhere_KeepingItsOwnNameInAnotherCase()
    {
        var id = await CreateAsync("Loyer");

        var updated = await CallAsync("update_line", Line($"{Prefix}LOYER", amount: 720m, day: 8, id: id));

        var line = LineNamed(updated, $"{Prefix}LOYER");
        Assert.Equal((id, 720m, 8), ((string)line["id"]!, (decimal)line["amount"]!, (int)line["dayOfMonth"]!));
    }

    [Fact]
    public async Task EndLine_KeepsItUntilTheDate_ThenRemovesItFromTheMonthsAfter()
    {
        var id = await CreateAsync("Salle de sport");

        var ended = await CallAsync("end_line", new JsonObject { ["lineId"] = id, ["endDate"] = "2031-10-31" });

        Assert.Equal("2031-10-31", (string?)LineNamed(ended, $"{Prefix}Salle de sport")["endDate"]);
        var november = (await _mcp.CallToolAsync("get_plan", new JsonObject { ["month"] = "2031-11" }))["structuredContent"]!;
        Assert.DoesNotContain(november["lines"]!.AsArray(), l => (string?)l!["id"] == id);
    }

    [Fact]
    public async Task DeleteLine_RemovesIt_WithItsCorrections()
    {
        var id = await CreateAsync("Loyer");
        await CallAsync("set_line_actual", new JsonObject { ["lineId"] = id, ["status"] = "Skipped" }, month: "2031-11");
        Assert.Equal(1, (int)LineNamed(await _mcp.CallToolAsync("get_plan", MonthOnly()), $"{Prefix}Loyer")["overridesEntered"]!);

        var deleted = await CallAsync("delete_line", new JsonObject { ["lineId"] = id, ["overridesEntered"] = 1 });

        Assert.DoesNotContain(deleted["structuredContent"]!["lines"]!.AsArray(), l => (string?)l!["id"] == id);
        Assert.DoesNotContain($"override {id}", await WrittenRows.SnapshotAsync(mcp));
    }

    [Fact]
    public async Task DeleteLine_WithoutCorrections_RemovesIt()
    {
        // The usual case: a line entered by mistake has no correction.
        var id = await CreateAsync("Loyer");

        var deleted = await CallAsync("delete_line", new JsonObject { ["lineId"] = id, ["overridesEntered"] = 0 });

        Assert.DoesNotContain(deleted["structuredContent"]!["lines"]!.AsArray(), l => (string?)l!["id"] == id);
        Assert.DoesNotContain($"line {id}", await WrittenRows.SnapshotAsync(mcp));
    }

    [Fact]
    public async Task ALineChangedFromAMonthOn_IsAnEndThenANewLineOfTheSameName_BothInThePlan()
    {
        var old = await CreateAsync("Loyer");
        await CallAsync("end_line", new JsonObject { ["lineId"] = old, ["endDate"] = "2031-12-31" });

        var next = await CallAsync("create_line", Line($"{Prefix}Loyer", amount: 700m, start: "2032-01-01"));

        // Seen from 2031-10: the line ended in December, and the next one, starting later.
        var lines = next["structuredContent"]!["lines"]!.AsArray().Where(l => (string?)l!["name"] == $"{Prefix}Loyer").ToList();
        Assert.Equal([("2031-01-01", 650m), ("2032-01-01", 700m)],
            lines.Select(l => ((string)l!["startDate"]!, (decimal)l["amount"]!)).Order());
    }

    [Fact]
    public async Task CreateLine_StartingAfterTheMonthShown_StaysInThePlan()
    {
        var created = await CallAsync("create_line", Line($"{Prefix}Mutuelle", start: "2031-12-01"));

        Assert.Equal("2031-12-01", (string?)LineNamed(created, $"{Prefix}Mutuelle")["startDate"]);
    }

    [Fact]
    public async Task ACorrection_IgnoredOnceItsLineEnded_CountsAgainWhenTheLineIsOpenAgain()
    {
        var id = await CreateAsync("Loyer");
        await CallAsync("set_line_actual", new JsonObject { ["lineId"] = id, ["status"] = "Skipped" }, month: "2031-11");

        var ended = await CallAsync("end_line", new JsonObject { ["lineId"] = id, ["endDate"] = "2031-10-31" });

        // Kept: still counted for a delete, but not a line of November any more.
        Assert.Equal(1, (int)LineNamed(ended, $"{Prefix}Loyer")["overridesEntered"]!);
        Assert.DoesNotContain(await SummaryLinesAsync("2031-11"), l => (string?)l!["id"] == id);

        await CallAsync("update_line", Line($"{Prefix}Loyer", id: id));

        Assert.Equal("Skipped", (string?)(await SummaryLinesAsync("2031-11")).Single(l => (string?)l!["id"] == id)!["status"]);
    }

    private async Task<JsonArray> SummaryLinesAsync(string month) =>
        (await _mcp.CallToolAsync("get_month_summary", new JsonObject { ["month"] = month }))["structuredContent"]!["lines"]!.AsArray();

    [Fact]
    public async Task CreateLine_WithAnInvalidDate_IsAToolError_NeverEchoingTheValue()
    {
        var result = await _mcp.CallToolAsync("create_line", WithMonth(Line($"{Prefix}Cadeaux", start: "not-a-date")));

        Assert.True((bool?)result["isError"]);
        Assert.DoesNotContain("not-a-date", Text(result));
    }

    [Fact]
    public async Task DeleteLine_WhenItsCorrectionsChangedSinceTheConfirmation_IsRefused_AndDeletesNothing()
    {
        var id = await CreateAsync("Loyer");
        await CallAsync("set_line_actual", new JsonObject { ["lineId"] = id, ["status"] = "Skipped" }, month: "2031-11");
        var before = await WrittenRows.SnapshotAsync(mcp);

        var result = await _mcp.CallToolAsync("delete_line", new JsonObject { ["month"] = Month, ["lineId"] = id, ["overridesEntered"] = 0 });

        Assert.True((bool?)result["isError"]);
        Assert.Contains("changed since the confirmation", Text(result));
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    [Fact]
    public async Task ANameTheDatabaseTakesForAnother_IsRefused_WithTheMessageOfTheDomain_AndWritesNothing()
    {
        // "Œ" and "OE": two names for the domain, the same one for the collation of SQL Server.
        await CreateAsync("Œuvres");
        var other = await CreateAsync("Cadeaux");
        var before = await WrittenRows.SnapshotAsync(mcp);

        var created = await _mcp.CallToolAsync("create_line", WithMonth(Line($"{Prefix}Oeuvres")));
        var renamed = await _mcp.CallToolAsync("update_line", WithMonth(Line($"{Prefix}OEUVRES", id: other)));

        Assert.All([created, renamed], result =>
        {
            Assert.True((bool?)result["isError"]);
            Assert.Contains("Another recurring line already has this name over the same period.", Text(result));
        });
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    public static TheoryData<string, string> InvalidLines => new()
    {
        { """{ "name": "  " }""", "Invalid recurring line" },
        { """{ "name": "LONG" }""", "Invalid recurring line" },
        { """{ "name": "Test line a\nb" }""", "Invalid recurring line" },
        // The name of a line of the demo, in another case.
        { """{ "name": "DEMO" }""", "Another recurring line already has this name over the same period." },
        { """{ "amount": 0 }""", "Invalid recurring line" },
        { """{ "amount": 12.345 }""", "Invalid recurring line" },
        // The bound written invariantly, whatever the culture of the server.
        { """{ "amount": 1000000000 }""", "999999999.99" },
        { """{ "dayOfMonth": 0 }""", "Invalid recurring line" },
        { """{ "dayOfMonth": 32 }""", "Invalid recurring line" },
        { """{ "anchorMonth": 3 }""", "Invalid recurring line" },
        { """{ "frequency": "Yearly", "anchorMonth": null }""", "Invalid recurring line" },
        { """{ "frequency": "Quarterly", "anchorMonth": 13 }""", "Invalid recurring line" },
        { """{ "endDate": "2030-12-31" }""", "Invalid recurring line" },
        { """{ "kind": "Income", "isSubscription": true }""", "Invalid recurring line" },
        { """{ "commitmentEndDate": "2031-12-31" }""", "commitmentEndDate is for a subscription only." },
        { """{ "kind": 7 }""", "Invalid recurring line" },
        { """{ "month": "2031-13" }""", "YYYY-MM" },
    };

    [Theory]
    [MemberData(nameof(InvalidLines))]
    public async Task CreateLine_Invalid_IsAToolError_AndWritesNothing(string changes, string expectedMessage)
    {
        var demo = await DemoLineNameAsync();
        var arguments = WithMonth(Line($"{Prefix}Cadeaux"));
        foreach (var (key, value) in JsonNode.Parse(changes)!.AsObject())
        {
            arguments[key] = (string?)(value as JsonValue)?.ToString() switch
            {
                "LONG" => Prefix + new string('x', 101),
                "DEMO" => demo.ToUpperInvariant(),
                _ => value?.DeepClone(),
            };
        }
        var before = await WrittenRows.SnapshotAsync(mcp);

        var result = await _mcp.CallToolAsync("create_line", arguments);

        Assert.True((bool?)result["isError"]);
        Assert.Contains(expectedMessage, Text(result));
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    [Fact]
    public async Task CreateLine_UnderTheNameOfABudget_IsAccepted()
    {
        var budget = (string)(await _mcp.CallToolAsync("get_plan", MonthOnly()))["structuredContent"]!["budgets"]!.AsArray()[0]!["name"]!;

        // Two lists apart: a budget and a line may share a name. Deleted after the test (not one of the demo lines).
        var created = await CallAsync("create_line", Line(budget));

        Assert.Single(created["structuredContent"]!["lines"]!.AsArray(), l => (string?)l!["name"] == budget);
    }

    public static TheoryData<string, string, string> InvalidCalls => new()
    {
        { "update_line", """{ "lineId": "00000000-0000-0000-0000-000000000099" }""", "No recurring line with this identifier in the plan." },
        { "update_line", """{ "lineId": "MINE", "name": "DEMO" }""", "Another recurring line already has this name over the same period." },
        { "update_line", """{ "lineId": "MINE", "dayOfMonth": 0 }""", "Invalid recurring line" },
        { "end_line", """{ "lineId": "MINE", "endDate": "2030-12-31" }""", "Invalid end date: never before the start date of the line." },
        { "end_line", """{ "lineId": "00000000-0000-0000-0000-000000000099", "endDate": "2031-12-31" }""", "No recurring line with this identifier in the plan." },
        { "delete_line", """{ "lineId": "00000000-0000-0000-0000-000000000099", "overridesEntered": 0 }""", "No recurring line with this identifier in the plan." },
    };

    [Theory]
    [MemberData(nameof(InvalidCalls))]
    public async Task InvalidCalls_AreToolErrors_AndWriteNothing(string tool, string changes, string expectedMessage)
    {
        var mine = await CreateAsync("Loyer");
        var demo = await DemoLineNameAsync();
        var arguments = tool == "update_line" ? WithMonth(Line($"{Prefix}Loyer", id: mine)) : new JsonObject { ["month"] = Month };
        foreach (var (key, value) in JsonNode.Parse(changes)!.AsObject())
        {
            arguments[key] = (string?)(value as JsonValue)?.ToString() switch
            {
                "MINE" => mine,
                "DEMO" => demo.ToLowerInvariant(),
                _ => value?.DeepClone(),
            };
        }
        var before = await WrittenRows.SnapshotAsync(mcp);

        var result = await _mcp.CallToolAsync(tool, arguments);

        Assert.True((bool?)result["isError"]);
        Assert.Contains(expectedMessage, Text(result));
        Assert.Equal(before, await WrittenRows.SnapshotAsync(mcp));
    }

    /// <summary>Every field of a line: a monthly expense from the start of 2031 by default, as the plan view sends it.</summary>
    private static JsonObject Line(string name, decimal amount = 650m, string frequency = "Monthly", int day = 5, int? anchorMonth = null,
        string start = "2031-01-01", string? end = null, string category = "Housing", bool subscription = false, string? commitmentEnd = null,
        string? id = null)
    {
        var line = new JsonObject
        {
            ["kind"] = "Expense",
            ["name"] = name,
            ["amount"] = amount,
            ["frequency"] = frequency,
            ["dayOfMonth"] = day,
            ["anchorMonth"] = anchorMonth,
            ["startDate"] = start,
            ["endDate"] = end,
            ["category"] = category,
            ["isSubscription"] = subscription,
            ["commitmentEndDate"] = commitmentEnd,
        };
        if (id is not null)
        {
            line["lineId"] = id;
        }

        return line;
    }

    /// <summary>The name of a line of the demo, found by its identifier: a line without end, running in every month.</summary>
    private async Task<string> DemoLineNameAsync() =>
        (string)(await _mcp.CallToolAsync("get_plan", MonthOnly()))["structuredContent"]!["lines"]!.AsArray()
            .First(l => mcp.DemoLineIds.Contains(Guid.Parse((string)l!["id"]!)))!["name"]!;

    /// <summary>Creates a monthly line named with the test prefix; returns its identifier.</summary>
    private async Task<string> CreateAsync(string name)
    {
        var created = await CallAsync("create_line", Line(Prefix + name));
        return (string)LineNamed(created, Prefix + name)["id"]!;
    }

    private async Task<JsonNode> CallAsync(string tool, JsonObject arguments, string month = Month)
    {
        arguments["month"] = month;
        var result = await _mcp.CallToolAsync(tool, arguments);
        Assert.NotEqual(true, (bool?)result["isError"]);
        return result;
    }

    private static JsonObject WithMonth(JsonObject arguments)
    {
        arguments["month"] = Month;
        return arguments;
    }

    private static JsonObject MonthOnly() => new() { ["month"] = Month };

    private static string Text(JsonNode result) => (string)result["content"]!.AsArray().Single()!["text"]!;

    private static JsonNode LineNamed(JsonNode result, string name) =>
        result["structuredContent"]!["lines"]!.AsArray().Single(l => (string?)l!["name"] == name)!;

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;
}
