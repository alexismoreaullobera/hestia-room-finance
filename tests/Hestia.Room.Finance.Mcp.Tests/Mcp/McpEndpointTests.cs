using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// End-to-end tests of the /mcp endpoint (in-memory server, real HTTP pipeline).
/// They lock the pitfalls found during the T1 spike (docs/spike-mcp-apps.md).
/// </summary>
public class McpEndpointTests(DevelopmentMcpServerFactory factory) : IClassFixture<DevelopmentMcpServerFactory>
{
    private const string BalanceViewUri = "ui://finance/balance.html";
    private const int RequestId = 1;

    [Fact]
    public async Task ToolsList_ExposesShowBalanceLinkedToItsView()
    {
        // Pitfall: WithTools(typeof(X)) compiled but registered no tool at all.
        var result = await SendAsync("tools/list");

        var tool = result["tools"]!.AsArray().Single(t => (string?)t!["name"] == "show_balance")!;
        Assert.Equal(BalanceViewUri, (string?)tool["_meta"]?["ui"]?["resourceUri"]);
    }

    [Fact]
    public async Task ResourcesRead_BalanceView_ReturnsMcpAppHtmlWithCsp()
    {
        // Pitfall: McpApps.SetResourceUi did not publish the CSP for a direct resource.
        var result = await SendAsync("resources/read", new JsonObject { ["uri"] = BalanceViewUri });

        var contents = Assert.Single(result["contents"]!.AsArray())!;
        Assert.Equal("text/html;profile=mcp-app", (string?)contents["mimeType"]);
        Assert.StartsWith("<!doctype html>", (string?)contents["text"]);
        var resourceDomains = contents["_meta"]?["ui"]?["csp"]?["resourceDomains"]?.AsArray();
        Assert.Contains("https://unpkg.com", resourceDomains!.Select(d => (string?)d));
    }

    [Fact]
    public async Task Request_WithForeignHostHeader_IsRejected()
    {
        // AllowedHosts=localhost in Development protects the local server against DNS rebinding.
        using var request = CreateRequest("tools/list");
        request.Headers.Host = "evil.example";

        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<JsonNode> SendAsync(string method, JsonObject? parameters = null)
    {
        using var request = CreateRequest(method, parameters);
        using var response = await factory.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Streamable HTTP: the Content-Type tells whether the answer is plain JSON or an SSE stream.
        var message = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
            ? FindResponseInSseStream(body, RequestId)
            : JsonNode.Parse(body)!;
        Assert.Null(message["error"]);
        return message["result"]!;
    }

    private static HttpRequestMessage CreateRequest(string method, JsonObject? parameters = null)
    {
        var payload = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = RequestId, ["method"] = method };
        if (parameters is not null)
        {
            payload["params"] = parameters;
        }

        var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        // Streamable HTTP: the client must accept both a plain JSON answer and an SSE stream.
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        return request;
    }

    /// <summary>
    /// Finds the JSON-RPC response matching <paramref name="requestId"/> in an SSE stream.
    /// The stream may also carry other messages (e.g. progress notifications), so the response
    /// is identified by its id, as JSON-RPC intends.
    /// </summary>
    private static JsonNode FindResponseInSseStream(string body, int requestId)
    {
        // SSE: events are separated by a blank line; an event's data is the concatenation of its "data:" lines.
        var events = body.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        foreach (var sseEvent in events)
        {
            var data = string.Join('\n', sseEvent
                .Split('\n')
                .Where(line => line.StartsWith("data:", StringComparison.Ordinal))
                .Select(line => line["data:".Length..])
                .Select(value => value.StartsWith(' ') ? value[1..] : value));

            if (data.Length > 0 && JsonNode.Parse(data) is { } message && (int?)message["id"] == requestId)
            {
                return message;
            }
        }

        throw new InvalidOperationException($"No JSON-RPC response with id {requestId} in the SSE stream:\n{body}");
    }
}
