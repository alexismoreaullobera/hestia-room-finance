using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Mcp.Tools;
using Hestia.Room.Finance.Mcp.Views;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// End-to-end tests of the views on the /mcp endpoint (in-memory server, real HTTP pipeline): the view tools, their
/// ui:// resources, and the pitfalls found during the T1 spike (docs/spike-mcp-apps.md).
/// </summary>
[Collection(McpServerCollection.Name)]
public partial class McpEndpointTests(McpServerFixture mcp)
{
    /// <summary>
    /// View tool, its data tool, its view, its title. The names and URIs are written as literals on purpose (not the
    /// constants of the code): they are a public contract read by the hosts, so renaming them must break these tests.
    /// </summary>
    private static readonly (string ViewTool, string DataTool, string ViewUri, string Title)[] ViewTable =
    [
        ("show_month_overview", "get_month_summary", "ui://finance/month-overview.html", "Vue : synthèse du mois"),
        ("show_cash_flow_forecast", "get_cash_flow_forecast", "ui://finance/cash-flow-forecast.html", "Vue : prévisionnel de trésorerie"),
        ("show_subscriptions", "list_subscriptions", "ui://finance/subscriptions.html", "Vue : abonnements"),
        ("show_plan", "get_plan", "ui://finance/plan.html", "Vue : plan"),
    ];

    private static readonly string[] DataTools = ["get_month_summary", "get_cash_flow_forecast", "list_recurring_lines", "list_subscriptions", "get_plan"];

    private static readonly string[] InputTools =
        ["set_starting_balance", "set_budget_actual", "set_line_actual", "create_budget", "update_budget", "delete_budget",
            "create_line", "update_line", "end_line", "delete_line"];

    /// <summary>The input tools each view calls from its forms (literals too: renaming one must break the views).</summary>
    private static readonly Dictionary<string, string[]> InputToolsOfViews = new()
    {
        ["ui://finance/cash-flow-forecast.html"] = ["set_starting_balance"],
        ["ui://finance/month-overview.html"] = ["set_budget_actual", "set_line_actual"],
        ["ui://finance/plan.html"] = ["create_budget", "update_budget", "delete_budget", "create_line", "update_line", "end_line", "delete_line"],
    };

    public static TheoryData<string, string, string> ViewToolsViewsAndTitles =>
        new(ViewTable.Select(v => (v.ViewTool, v.ViewUri, v.Title)));

    public static TheoryData<string, string> ViewToolsAndDataTools => new(ViewTable.Select(v => (v.ViewTool, v.DataTool)));

    public static TheoryData<string, string> ViewToolsAndViews => new(ViewTable.Select(v => (v.ViewTool, v.ViewUri)));

    public static TheoryData<string> ViewUris => new(ViewTable.Select(v => v.ViewUri));

    /// <summary>
    /// Every view tool with a valid month, no month (the first display by the agent), and invalid months the view
    /// could send (0000-12 is the month before 0001-01, 10000-01 the month after 9999-12).
    /// </summary>
    public static TheoryData<string, string, string?> ViewToolsDataToolsAndMonths => new(
        from view in ViewTable
        from month in new[] { DevelopmentMcpServerFactory.CurrentMonth.AddMonths(-1).ToString(), null, "2026-13", "0000-12", "10000-01" }
        select (view.ViewTool, view.DataTool, month));

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Fact]
    public async Task TheServer_ExposesExactlyTheDataToolsTheViewToolsAndTheInputTools()
    {
        // Pitfall: WithTools(typeof(X)) compiled but registered no tool at all. Also catches the spike coming back.
        var names = (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Select(t => (string)t!["name"]!);

        Assert.Equal(DataTools.Concat(ViewTable.Select(v => v.ViewTool)).Concat(InputTools).Order(), names.Order());
    }

    [Fact]
    public async Task OnlyTheInputTools_AreHiddenFromTheModel()
    {
        // The agent has no write access in the v1: every tool it is offered reads, every write is for the views.
        var tools = (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray();
        var appOnly = tools.Where(t => t!["_meta"]?["ui"]?["visibility"]?.AsArray().Select(v => (string?)v).SequenceEqual(["app"]) == true);
        var writes = tools.Where(t => (bool?)t!["annotations"]?["readOnlyHint"] != true);

        Assert.Equal(InputTools.Order(), appOnly.Select(t => (string)t!["name"]!).Order());
        Assert.Equal(InputTools.Order(), writes.Select(t => (string)t!["name"]!).Order());
    }

    [Fact]
    public async Task TheViewTable_IsExactlyWhatTheServerExposes()
    {
        // A view registered or embedded but missing from this table would escape every guard below.
        var uiResources = (await _mcp.SendAsync("resources/list"))["resources"]!.AsArray()
            .Select(r => (string)r!["uri"]!).Where(uri => uri.StartsWith("ui://", StringComparison.Ordinal));
        var toolsWithAView = (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray()
            .Where(t => t!["_meta"]?["ui"]?["resourceUri"] is not null).Select(t => (string)t!["name"]!);
        var embeddedViews = typeof(FinanceViewResources).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("views/", StringComparison.Ordinal) && name.EndsWith(".html", StringComparison.Ordinal));

        Assert.Equal(ViewTable.Select(v => v.ViewUri).Order(), uiResources.Order());
        Assert.Equal(ViewTable.Select(v => v.ViewTool).Order(), toolsWithAView.Order());
        Assert.Equal(ViewTable.Length, embeddedViews.Count());
    }

    [Theory]
    [MemberData(nameof(ViewToolsViewsAndTitles))]
    public async Task ToolsList_LinksAViewToolToItsView_WithItsTitle_AndAnOptionalMonth(string viewTool, string viewUri, string title)
    {
        var tool = await ToolAsync(viewTool);

        Assert.Equal(viewUri, (string?)tool["_meta"]?["ui"]?["resourceUri"]);
        // Hosts show the title of a tool; the agent calls show_* without a month to show the current one.
        Assert.Equal(title, (string?)tool["annotations"]?["title"] ?? (string?)tool["title"]);
        Assert.Equal(["month"], tool["inputSchema"]!["properties"]!.AsObject().Select(p => p.Key));
        Assert.DoesNotContain("month", tool["inputSchema"]?["required"]?.AsArray().Select(r => (string?)r) ?? []);
    }

    [Theory]
    [MemberData(nameof(ViewToolsAndDataTools))]
    public async Task ToolsList_AViewTool_HasTheOutputSchemaAndTheHintsOfItsDataTool(string viewTool, string dataTool)
    {
        var view = await ToolAsync(viewTool);
        var data = await ToolAsync(dataTool);

        Assert.True(JsonNode.DeepEquals(data["outputSchema"], view["outputSchema"]), $"{viewTool} schema differs from {dataTool}.");
        // readOnlyHint, idempotentHint, openWorldHint: hosts rely on them to call a tool without asking.
        Assert.True(JsonNode.DeepEquals(HintsOf(data), HintsOf(view)), $"{viewTool} hints differ from {dataTool}.");
        Assert.True((bool?)view["annotations"]?["readOnlyHint"]);
    }

    [Theory]
    [MemberData(nameof(ViewToolsDataToolsAndMonths))]
    public async Task ToolsCall_AViewTool_ReturnsExactlyTheResultOfItsDataTool(string viewTool, string dataTool, string? month)
    {
        JsonObject Arguments() => month is null ? [] : new JsonObject { ["month"] = month };

        var viewResult = await _mcp.CallToolAsync(viewTool, Arguments());
        var dataResult = await _mcp.CallToolAsync(dataTool, Arguments());

        if (month is not null && !YearMonth.TryParse(month, out _))
        {
            // What the view shows as "Données indisponibles": a tool error, no structured content, and the same
            // message as the data tool, but for the tool name it quotes.
            Assert.True((bool?)viewResult["isError"]);
            Assert.Null(viewResult["structuredContent"]);
            Assert.True((bool?)dataResult["isError"]);
            Assert.Equal(Text(dataResult), Text(viewResult).Replace($"'{viewTool}'", $"'{dataTool}'"));
            return;
        }
        Assert.True(JsonNode.DeepEquals(dataResult, viewResult), $"{viewTool} differs from {dataTool} for month '{month}'.");
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task ResourcesList_AView_HasItsMetadataAndItsCsp(string viewUri)
    {
        // Same pitfall as resources/read: some hosts read the CSP from the resource list.
        var result = await _mcp.SendAsync("resources/list");

        var view = result["resources"]!.AsArray().Single(r => (string?)r!["uri"] == viewUri)!;
        Assert.Equal("text/html;profile=mcp-app", (string?)view["mimeType"]);
        Assert.NotNull(view["_meta"]?["ui"]?["csp"]);
        Assert.Empty(CspDomains(view));
        Assert.All(new[] { "name", "title", "description" }, key => Assert.False(string.IsNullOrWhiteSpace((string?)view[key])));
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task ResourcesRead_AView_ReturnsMcpAppHtmlWithCsp(string viewUri)
    {
        // Pitfall: McpApps.SetResourceUi did not publish the CSP for a direct resource.
        var contents = await ReadViewAsync(viewUri);

        Assert.Equal("text/html;profile=mcp-app", (string?)contents["mimeType"]);
        Assert.StartsWith("<!doctype html>", (string)contents["text"]!, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(contents["_meta"]?["ui"]?["csp"]);
        Assert.Empty(CspDomains(contents));
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task AView_IsSelfContained_LoadingNothingFromOutside(string viewUri)
    {
        // The build inlines the scripts, the styles and the App SDK: no CDN, no third-party code loaded at runtime.
        // The markup rules (no innerHTML, attributes through the allowlist) are checked by ESLint and the JS tests.
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;

        Assert.Single(ModuleScript().Matches(html));
        Assert.DoesNotMatch(ExternalLoad(), html);
        Assert.DoesNotContain("<link", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [MemberData(nameof(ViewToolsAndViews))]
    public async Task AView_CallsOnlyItsOwnTools_AndHasEveryPageElement(string viewTool, string viewUri)
    {
        // A view wired to another tool would navigate into the wrong view or write to the wrong figure; a page without
        // one of the ids that startView requires would stay blank. The ids come from view.ts, the single list both
        // sides read. The tools looked for are the ones the server exposes: the SDK bundled in the view has its own
        // words (list_changed), which are no tool.
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;
        var view = File.ReadAllText(Path.Combine(ViewFixturesTests.ViewsProjectDirectory(), "src", "shared", "view.ts"));
        var pageIds = Quoted().Matches(PageIds().Match(view).Groups[1].Value).Select(m => m.Groups[1].Value).ToList();
        var serverTools = (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Select(t => (string)t!["name"]!);

        var mentioned = serverTools.Where(name => Regex.IsMatch(html, $@"\b{Regex.Escape(name)}\b"));

        Assert.Equal(InputToolsOfViews.GetValueOrDefault(viewUri, []).Append(viewTool).Order(), mentioned.Order());
        Assert.Equal(["label", "previous", "month", "next", "status", "content"], pageIds);
        Assert.All(pageIds, id => Assert.Contains($"id=\"{id}\"", html));
    }

    [Fact]
    public void ViewsOutputs_DeclareExactlyTheEnumsOfTheOutputs()
    {
        // The TypeScript unions of outputs.ts equal the domain enums; the views translate every value of the
        // unions (a Record over them), so a new value of the domain cannot show up untranslated.
        var outputs = File.ReadAllText(Path.Combine(ViewFixturesTests.ViewsProjectDirectory(), "src", "shared", "outputs.ts"));

        Assert.Equal(Enum.GetNames<Frequency>().Order(), Union(outputs, "Frequency").Order());
        Assert.Equal(Enum.GetNames<Category>().Order(), Union(outputs, "Category").Order());
        Assert.Equal(Enum.GetNames<FlowKind>().Order(), Union(outputs, "FlowKind").Order());
        Assert.Equal(Enum.GetNames<LineActualStatus>().Order(), Union(outputs, "LineActualStatus").Order());
    }

    [Fact]
    public async Task Request_WithForeignHostHeader_IsRejected()
    {
        // AllowedHosts=localhost in Development protects the local server against DNS rebinding.
        using var request = McpTestClient.CreateRequest("tools/list");
        request.Headers.Host = "evil.example";

        using var response = await _mcp.Http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Request_FromAForeignOrigin_IsRefused_AndWritesNothing()
    {
        // A web page of another site making the browser call the server: refused before any tool runs.
        using var request = McpTestClient.CreateRequest("tools/call", SetStartingBalance("2031-06", 10m));
        request.Headers.Add("Origin", "https://evil.example");

        using var response = await _mcp.Http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0m, await StartingBalanceAsync("2031-06"));
    }

    [Theory]
    [InlineData("http://localhost:8080")]
    [InlineData("http://localhost:6274")]
    public async Task Request_FromAnAllowedHostOrigin_IsServed(string origin)
    {
        // The origins of the local web hosts allowed in Development (Cors:DevMcpHostOrigins): a future Hestia shell, and
        // the MCP Inspector, whose page sends its own origin (refused by T9 until the Inspector check of T10).
        using var request = McpTestClient.CreateRequest("tools/list");
        request.Headers.Add("Origin", origin);

        using var response = await _mcp.Http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Request_AsPlainText_IsRefused_AndWritesNothing()
    {
        // What a cross-site HTML form or a "simple" fetch can send without a CORS preflight: never a call.
        using var request = McpTestClient.CreateRequest("tools/call", SetStartingBalance("2031-07", 10m));
        request.Content = new StringContent(await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken),
            System.Text.Encoding.UTF8, "text/plain");

        using var response = await _mcp.Http.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.False(response.IsSuccessStatusCode);
        Assert.Equal(0m, await StartingBalanceAsync("2031-07"));
    }

    private static JsonObject SetStartingBalance(string month, decimal startingBalance) => new()
    {
        ["name"] = "set_starting_balance",
        ["arguments"] = new JsonObject { ["month"] = month, ["startingBalance"] = startingBalance },
    };

    private async Task<decimal?> StartingBalanceAsync(string month) =>
        (decimal?)(await _mcp.CallToolAsync("get_cash_flow_forecast", new JsonObject { ["month"] = month }))["structuredContent"]!["startingBalance"];

    [GeneratedRegex(@"<script type=""module""")]
    private static partial Regex ModuleScript();

    // A src or href attribute pointing to another origin (any scheme, or protocol relative).
    [GeneratedRegex(@"\b(?:src|href)\s*=\s*[""']?(?:[a-z][a-z0-9+.-]*:)?//", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalLoad();

    [GeneratedRegex(@"PAGE_IDS = \[([^\]]+)\]")]
    private static partial Regex PageIds();

    [GeneratedRegex(@"""(\w+)""")]
    private static partial Regex Quoted();

    /// <summary>The values of a union of string literals in outputs.ts (export type Name = "A" | "B";).</summary>
    private static IEnumerable<string> Union(string outputs, string name)
    {
        var union = Regex.Match(outputs, $@"export type {name} =([^;]+);");
        Assert.True(union.Success, $"outputs.ts declares no union {name}.");
        return Quoted().Matches(union.Groups[1].Value).Select(m => m.Groups[1].Value);
    }

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;

    private async Task<JsonNode> ReadViewAsync(string uri) =>
        Assert.Single((await _mcp.SendAsync("resources/read", new JsonObject { ["uri"] = uri }))["contents"]!.AsArray())!;

    private static string Text(JsonNode result) => (string)result["content"]!.AsArray().Single()!["text"]!;

    /// <summary>Every domain the CSP of a view allows, whatever the directive: none, the views being self-contained.</summary>
    private static IEnumerable<string?> CspDomains(JsonNode resource) =>
        resource["_meta"]?["ui"]?["csp"]?.AsObject().SelectMany(directive => directive.Value as JsonArray ?? []).Select(d => (string?)d) ?? [];

    /// <summary>The annotations of a tool without its title: the hints a view tool shares with its data tool.</summary>
    private static JsonObject HintsOf(JsonNode tool)
    {
        var hints = tool["annotations"]!.DeepClone().AsObject();
        hints.Remove("title");
        return hints;
    }
}
