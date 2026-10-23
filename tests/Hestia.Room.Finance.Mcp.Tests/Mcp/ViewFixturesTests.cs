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

    // Identifiers change with every seed (Guid v7): replaced by a constant, they carry no information for a view.
    private const string FixedId = "00000000-0000-0000-0000-000000000000";

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

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
            await File.WriteAllTextAsync(path, expected, TestContext.Current.CancellationToken);
        }

        Assert.True(File.Exists(path), $"Missing fixture {path}: run this test with {UpdateVariable}=1.");
        var actual = (await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n");
        Assert.True(expected == actual, $"{fixture} is out of date: run this test with {UpdateVariable}=1, then update the views.");
    }

    private static JsonNode WithFixedIds(JsonNode node)
    {
        var copy = node.DeepClone();
        foreach (var item in copy["subscriptions"]?.AsArray() ?? [])
        {
            item!["id"] = FixedId;
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
