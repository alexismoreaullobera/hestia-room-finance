using System.Net;
using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// End-to-end tests of the /mcp endpoint (in-memory server, real HTTP pipeline).
/// They lock the pitfalls found during the T1 spike (docs/spike-mcp-apps.md).
/// </summary>
[Collection(McpServerCollection.Name)]
public class McpEndpointTests(McpServerFixture mcp)
{
    // Written as a literal on purpose (not FinanceViewResources.BalanceViewUri): this URI is a public
    // contract read by the hosts, so renaming it in the code must break these tests.
    private const string BalanceViewUri = "ui://finance/balance.html";

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Fact]
    public async Task ToolsList_ExposesShowBalanceLinkedToItsView()
    {
        // Pitfall: WithTools(typeof(X)) compiled but registered no tool at all.
        var result = await _mcp.SendAsync("tools/list");

        var tool = result["tools"]!.AsArray().Single(t => (string?)t!["name"] == "show_balance")!;
        Assert.Equal(BalanceViewUri, (string?)tool["_meta"]?["ui"]?["resourceUri"]);
    }

    [Fact]
    public async Task ToolsList_ShowBalance_IsAnnouncedAsReadOnly()
    {
        // Hosts and agents rely on readOnlyHint to call a tool without asking for confirmation.
        var result = await _mcp.SendAsync("tools/list");

        var tool = result["tools"]!.AsArray().Single(t => (string?)t!["name"] == "show_balance")!;
        Assert.True((bool?)tool["annotations"]?["readOnlyHint"]);
    }

    [Fact]
    public async Task ResourcesList_BalanceView_DeclaresItsCsp()
    {
        // Same pitfall as resources/read: some hosts read the CSP from the resource list.
        var result = await _mcp.SendAsync("resources/list");

        var view = result["resources"]!.AsArray().Single(r => (string?)r!["uri"] == BalanceViewUri)!;
        var resourceDomains = view["_meta"]?["ui"]?["csp"]?["resourceDomains"]?.AsArray();
        Assert.NotNull(resourceDomains);
        Assert.Contains("https://unpkg.com", resourceDomains.Select(d => (string?)d));
    }

    [Fact]
    public async Task ResourcesRead_BalanceView_ReturnsMcpAppHtmlWithCsp()
    {
        // Pitfall: McpApps.SetResourceUi did not publish the CSP for a direct resource.
        var result = await _mcp.SendAsync("resources/read", new JsonObject { ["uri"] = BalanceViewUri });

        var contents = Assert.Single(result["contents"]!.AsArray())!;
        Assert.Equal("text/html;profile=mcp-app", (string?)contents["mimeType"]);
        Assert.StartsWith("<!doctype html>", (string?)contents["text"], StringComparison.OrdinalIgnoreCase);
        var resourceDomains = contents["_meta"]?["ui"]?["csp"]?["resourceDomains"]?.AsArray();
        Assert.NotNull(resourceDomains);
        Assert.Contains("https://unpkg.com", resourceDomains.Select(d => (string?)d));
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
}
