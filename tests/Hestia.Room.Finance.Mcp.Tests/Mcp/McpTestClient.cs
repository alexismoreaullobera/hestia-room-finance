using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// Minimal JSON-RPC client for the /mcp endpoint, written by hand on purpose: the tests see the exact messages
/// a host sends and receives (Streamable HTTP, plain JSON or SSE answer).
/// </summary>
public sealed class McpTestClient(HttpClient client)
{
    private const int RequestId = 1;

    /// <summary>The underlying client, for the tests that build their own HTTP request.</summary>
    public HttpClient Http => client;

    /// <summary>Sends a request and returns its JSON-RPC <c>result</c>; a JSON-RPC error fails the test.</summary>
    public async Task<JsonNode> SendAsync(string method, JsonObject? parameters = null)
    {
        using var request = CreateRequest(method, parameters);
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        // Streamable HTTP: the Content-Type tells whether the answer is plain JSON or an SSE stream.
        var message = response.Content.Headers.ContentType?.MediaType == "text/event-stream"
            ? FindResponseInSseStream(body, RequestId)
            : JsonNode.Parse(body)!;
        Assert.True(message["error"] is null, $"JSON-RPC error: {message["error"]?.ToJsonString()}");
        return message["result"]!;
    }

    /// <summary>Calls a tool; the result may be a tool error (<c>isError</c>), which is not a JSON-RPC error.</summary>
    public Task<JsonNode> CallToolAsync(string name, JsonObject? arguments = null) =>
        SendAsync("tools/call", new JsonObject { ["name"] = name, ["arguments"] = arguments ?? new JsonObject() });

    public static HttpRequestMessage CreateRequest(string method, JsonObject? parameters = null)
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
