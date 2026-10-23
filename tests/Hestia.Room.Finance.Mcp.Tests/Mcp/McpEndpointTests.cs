using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// End-to-end tests of the views on the /mcp endpoint (in-memory server, real HTTP pipeline): the view tools, their
/// ui:// resources, and the pitfalls found during the T1 spike (docs/spike-mcp-apps.md).
/// </summary>
[Collection(McpServerCollection.Name)]
public class McpEndpointTests(McpServerFixture mcp)
{
    /// <summary>
    /// View tool, its data tool, its view. The URIs are written as literals on purpose (not the constants of the
    /// code): they are a public contract read by the hosts, so renaming them in the code must break these tests.
    /// </summary>
    private static readonly (string ViewTool, string DataTool, string ViewUri)[] ViewTable =
    [
        ("show_month_overview", "get_month_summary", "ui://finance/month-overview.html"),
        ("show_cash_flow_forecast", "get_cash_flow_forecast", "ui://finance/cash-flow-forecast.html"),
        ("show_subscriptions", "list_subscriptions", "ui://finance/subscriptions.html"),
    ];

    public static TheoryData<string, string> ViewToolsAndViews => new(ViewTable.Select(v => (v.ViewTool, v.ViewUri)));

    public static TheoryData<string, string> ViewToolsAndDataTools => new(ViewTable.Select(v => (v.ViewTool, v.DataTool)));

    public static TheoryData<string> ViewUris => new(ViewTable.Select(v => v.ViewUri));

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Theory]
    [MemberData(nameof(ViewToolsAndViews))]
    public async Task ToolsList_LinksAViewToolToItsView_ReadOnly_WithAMonthArgument(string viewTool, string viewUri)
    {
        // Pitfall: WithTools(typeof(X)) compiled but registered no tool at all.
        var tool = await ToolAsync(viewTool);

        Assert.Equal(viewUri, (string?)tool["_meta"]?["ui"]?["resourceUri"]);
        // Hosts and agents rely on readOnlyHint to call a tool without asking for confirmation.
        Assert.True((bool?)tool["annotations"]?["readOnlyHint"]);
        Assert.Equal(["month"], tool["inputSchema"]!["properties"]!.AsObject().Select(p => p.Key));
    }

    [Theory]
    [MemberData(nameof(ViewToolsAndDataTools))]
    public async Task ToolsList_AViewTool_AdvertisesTheOutputSchemaOfItsDataTool(string viewTool, string dataTool)
    {
        Assert.True(JsonNode.DeepEquals((await ToolAsync(dataTool))["outputSchema"], (await ToolAsync(viewTool))["outputSchema"]));
    }

    [Theory]
    [MemberData(nameof(ViewToolsAndDataTools))]
    public async Task ToolsCall_AViewTool_ReturnsExactlyTheResultOfItsDataTool(string viewTool, string dataTool)
    {
        var arguments = new JsonObject { ["month"] = DevelopmentMcpServerFactory.CurrentMonth.AddMonths(-1).ToString() };

        var viewResult = await _mcp.CallToolAsync(viewTool, arguments);
        var dataResult = await _mcp.CallToolAsync(dataTool, (JsonObject)arguments.DeepClone());

        Assert.True(JsonNode.DeepEquals(dataResult, viewResult), $"{viewTool} differs from {dataTool}.");
    }

    [Fact]
    public async Task ToolsList_NoLongerExposesTheSpike()
    {
        var names = (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Select(t => (string?)t!["name"]);

        Assert.DoesNotContain("show_balance", names);
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task ResourcesList_AView_DeclaresItsCsp(string viewUri)
    {
        // Same pitfall as resources/read: some hosts read the CSP from the resource list.
        var result = await _mcp.SendAsync("resources/list");

        var view = result["resources"]!.AsArray().Single(r => (string?)r!["uri"] == viewUri)!;
        Assert.Equal("text/html;profile=mcp-app", (string?)view["mimeType"]);
        Assert.Contains("https://unpkg.com", ResourceDomains(view));
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task ResourcesRead_AView_ReturnsMcpAppHtmlWithCspAndTheSharedCode(string viewUri)
    {
        // Pitfall: McpApps.SetResourceUi did not publish the CSP for a direct resource.
        var contents = await ReadViewAsync(viewUri);

        var html = (string)contents["text"]!;
        Assert.Equal("text/html;profile=mcp-app", (string?)contents["mimeType"]);
        Assert.StartsWith("<!doctype html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("https://unpkg.com", ResourceDomains(contents));
        // The shared code is injected in place of its markers: the view connects to its host through it.
        Assert.DoesNotContain("@shared", html);
        Assert.Contains("import { App } from", html);
        Assert.Contains("startView(", html);
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task AView_NeverWritesHtmlFromData(string viewUri)
    {
        // Tool data is untrusted (a name typed by the user, prompt injection): views set textContent only.
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;

        Assert.All(new[] { "innerHTML", "outerHTML", "insertAdjacentHTML", "document.write" },
            api => Assert.DoesNotContain(api, html));
    }

    [Theory]
    [MemberData(nameof(ViewUris))]
    public async Task AView_LoadsNothingFromADomainOutsideItsCsp(string viewUri)
    {
        var contents = await ReadViewAsync(viewUri);
        var declared = ResourceDomains(contents).ToHashSet();

        var used = Regex.Matches((string)contents["text"]!, @"https://[A-Za-z0-9.-]+").Select(m => m.Value).Distinct();

        Assert.All(used, origin => Assert.Contains(origin, declared));
    }

    [Theory]
    [MemberData(nameof(ViewToolsAndViews))]
    public async Task AView_ChangesMonthThroughItsOwnTool(string viewTool, string viewUri)
    {
        // The previous and next buttons call this tool with the month: it must be the tool that links to the view.
        var html = (string)(await ReadViewAsync(viewUri))["text"]!;

        Assert.Contains($"tool: \"{viewTool}\"", html);
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

    private async Task<JsonNode> ToolAsync(string name) =>
        (await _mcp.SendAsync("tools/list"))["tools"]!.AsArray().Single(t => (string?)t!["name"] == name)!;

    private async Task<JsonNode> ReadViewAsync(string uri) =>
        Assert.Single((await _mcp.SendAsync("resources/read", new JsonObject { ["uri"] = uri }))["contents"]!.AsArray())!;

    private static IEnumerable<string?> ResourceDomains(JsonNode resource) =>
        resource["_meta"]?["ui"]?["csp"]?["resourceDomains"]?.AsArray().Select(d => (string?)d) ?? [];
}
