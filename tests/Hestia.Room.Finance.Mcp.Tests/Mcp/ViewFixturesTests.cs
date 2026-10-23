using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Hestia.Room.Finance.Mcp.Tests.Mcp;

/// <summary>
/// The contract between the server and the views: the JSON fixtures rendered by the JavaScript tests
/// (src/Hestia.Room.Finance.Views/test/fixtures) are the structured content of the view tools on the demo plan.
/// If an output changes, this test fails until the fixtures are written again, and the TypeScript interfaces and
/// tests follow. To write them: set HESTIA_UPDATE_VIEW_FIXTURES=1 and run this test once.
/// </summary>
[Collection(McpServerCollection.Name)]
public class ViewFixturesTests(McpServerFixture mcp)
{
    private const string UpdateVariable = "HESTIA_UPDATE_VIEW_FIXTURES";

    // Readable fixtures ("Électricité", not \u00C9lectricit\u00E9): read by JSON.parse, never inserted in markup.
    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly McpTestClient _mcp = new(mcp.Server.CreateClient());

    [Theory]
    [InlineData("show_month_overview", "month-overview.json")]
    [InlineData("show_cash_flow_forecast", "cash-flow-forecast.json")]
    [InlineData("show_subscriptions", "subscriptions.json")]
    public async Task Fixture_IsTheStructuredContentOfTheViewTool(string viewTool, string fixture)
    {
        var content = (await _mcp.CallToolAsync(viewTool))["structuredContent"]!;
        var expected = WithFixedIds(content).ToJsonString(Indented).ReplaceLineEndings("\n") + "\n";
        var path = Path.Combine(ViewsProjectDirectory(), "test", "fixtures", fixture);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            // Never in the CI: there, an out-of-date fixture must fail, not be rewritten.
            Assert.False(Environment.GetEnvironmentVariable("CI") == "true", $"{UpdateVariable} must not be set in the CI.");
            await File.WriteAllTextAsync(path, expected, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(path), $"Missing fixture {path}: run this test with {UpdateVariable}=1.");
        var actual = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        // Out of date: run this test with HESTIA_UPDATE_VIEW_FIXTURES=1, then update the views.
        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// Identifiers change with every seed (Guid v7): replaced by stable ones, distinct for each item, so that a view
    /// keyed on them would still see different identifiers.
    /// </summary>
    private static JsonNode WithFixedIds(JsonNode node)
    {
        var copy = node.DeepClone();
        var index = 0;
        foreach (var item in copy["subscriptions"]?.AsArray() ?? [])
        {
            item!["id"] = $"00000000-0000-0000-0000-{++index:D12}";
        }
        return copy;
    }

    /// <summary>The views project, found from the test binaries up to the solution file.</summary>
    internal static string ViewsProjectDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Hestia.Room.Finance.slnx")))
        {
            directory = directory.Parent;
        }
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "Hestia.Room.Finance.Views");
    }
}
