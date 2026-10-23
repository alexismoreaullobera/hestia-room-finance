using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Hestia.Room.Finance.Domain;
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
    ];

    private static readonly string[] DataTools = ["get_month_summary", "get_cash_flow_forecast", "list_recurring_lines", "list_subscriptions"];

    public static TheoryData<string, string, string> ViewToolsViewsAndTitles =>
        new(ViewTable.Select(v => (v.ViewTool, v.ViewUri, v.Title)));

    public static TheoryData<string, string> ViewToolsAndDataTools => new(ViewTable.Select(v => (v.ViewTool, v.DataTool)));

    public static TheoryData<string, string> ViewToolsAndViews => new(ViewTable.Select(v => (v.ViewTool, v.ViewUri)));

    public static TheoryData<string> ViewUris => new(ViewTable.Select(v => v.ViewUri));

    /// <summary>
    /// Every view tool with a valid month, no month (the first display by the agent), and invalid months the view
    /// could send (0000-12 is the month before 0001-01).
    /// </summary>
    public static TheoryData<string, string, string?> ViewToolsDataToolsAndMonths => new(
        from view in ViewTable
        from month in new[] { DevelopmentMcpServerFactory.CurrentMonth.AddMonths(-1).ToString(), null, "2026-13", "0000-12" }
        select (view.ViewTool, view.DataTool, month));

    // Every API able to turn a string into markup or code: none may appear in a view.
    private static readonly string[] HtmlAndCodeSinks =
    [
        "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write", "setHTMLUnsafe", "createContextualFragment",
        "DOMParser", "parseFromString", "srcdoc", "eval(", "new Function", "import(", "javascript:",
    ];

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Fact]
    public async Task TheServer_ExposesExactlyTheDataToolsAndTheViewTools()
    {
        // Pitfall: WithTools(typeof(X)) compiled but registered no tool at all. Also catches the spike coming back.
        var names = (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Select(t => (string)t!["name"]!);

        Assert.Equal(DataTools.Concat(ViewTable.Select(v => v.ViewTool)).Order(), names.Order());
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
        Assert.Contains("https://unpkg.com", ResourceDomains(view));
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
        Assert.Contains("https://unpkg.com", ResourceDomains(contents));
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task ResourcesRead_AView_HasTheSharedCodeInPlace(string viewUri)
    {
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;

        // The shared styles in the style block; the shared script first in the module (its import must stay first).
        Assert.Contains(Embedded("views/shared/view.css"), StyleBlock().Match(html).Groups[1].Value);
        Assert.StartsWith(Embedded("views/shared/view.js"), ModuleBlock().Match(html).Groups[1].Value.TrimStart());
        Assert.DoesNotContain("@shared", html);
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task AView_NeverTurnsDataIntoMarkupOrCode(string viewUri)
    {
        // Tool data is untrusted (a name typed by the user, prompt injection): views set text only.
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;

        Assert.All(HtmlAndCodeSinks, sink => Assert.DoesNotContain(sink, html));
        // Every attribute goes through the allowlist of the shared kit: a single setAttribute call, in setAttributes().
        Assert.Single(SetAttributeCall().Matches(html));
        Assert.Contains("SAFE_ATTRIBUTES.has(name)", html);
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task AView_UsesExactlyTheDomainsOfItsCsp(string viewUri)
    {
        var contents = await ReadViewAsync(viewUri);

        // Any scheme and protocol-relative URLs, with their port; the SVG namespace is a name, not a load.
        var used = ExternalUrl().Matches((string)contents["text"]!)
            .Select(m => m.Groups["origin"].Value)
            .Select(origin => origin.StartsWith("//", StringComparison.Ordinal) ? "https:" + origin : origin)
            .Where(origin => origin != "http://www.w3.org")
            .ToHashSet();

        // Equal, not only included: the CSP stays minimal, and this test cannot go empty without failing.
        Assert.Equal(ResourceDomains(contents).Order(), used.Order());
    }

    [Theory]
    [MemberData(nameof(ViewToolsAndViews))]
    public async Task AView_ChangesMonthThroughItsOwnTool_Only(string viewTool, string viewUri)
    {
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;

        // The previous and next buttons call view.tool, which must be the tool that links to this view.
        Assert.Equal(viewTool, Assert.Single(ViewToolDeclaration().Matches(html)).Groups["tool"].Value);
        Assert.Single(CallServerTool().Matches(html));
        Assert.Single(CallServerToolWithViewTool().Matches(html));
    }

    [Fact]
    public async Task SubscriptionsView_TranslatesEveryFrequencyAndCategory()
    {
        // A new value of the domain enums must not show up in English in the view.
        var html = (string)(await ReadViewAsync("ui://finance/subscriptions.html"))["text"]!;

        Assert.All(Enum.GetNames<Frequency>().Concat(Enum.GetNames<Category>()), name => Assert.Contains($"{name}: \"", html));
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

    [GeneratedRegex("<style>(.*?)</style>", RegexOptions.Singleline)]
    private static partial Regex StyleBlock();

    [GeneratedRegex("<script type=\"module\">(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex ModuleBlock();

    [GeneratedRegex(@"\bsetAttribute\(")]
    private static partial Regex SetAttributeCall();

    [GeneratedRegex(@"(?<origin>(?:\b(?:https?|wss?):)?//[A-Za-z0-9.-]+\.[A-Za-z]{2,}(?::\d+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrl();

    [GeneratedRegex(@"\btool:\s*[""'](?<tool>\w+)[""']")]
    private static partial Regex ViewToolDeclaration();

    [GeneratedRegex(@"\bcallServerTool\(")]
    private static partial Regex CallServerTool();

    [GeneratedRegex(@"\bcallServerTool\(\{\s*name:\s*view\.tool\b")]
    private static partial Regex CallServerToolWithViewTool();

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;

    private async Task<JsonNode> ReadViewAsync(string uri) =>
        Assert.Single((await _mcp.SendAsync("resources/read", new JsonObject { ["uri"] = uri }))["contents"]!.AsArray())!;

    private static string Text(JsonNode result) => (string)result["content"]!.AsArray().Single()!["text"]!;

    private static IEnumerable<string?> ResourceDomains(JsonNode resource) =>
        resource["_meta"]?["ui"]?["csp"]?["resourceDomains"]?.AsArray().Select(d => (string?)d) ?? [];

    /// <summary>The annotations of a tool without its title: the hints a view tool shares with its data tool.</summary>
    private static JsonObject HintsOf(JsonNode tool)
    {
        var hints = tool["annotations"]!.DeepClone().AsObject();
        hints.Remove("title");
        return hints;
    }

    private static string Embedded(string name)
    {
        using var stream = typeof(FinanceViewResources).Assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
