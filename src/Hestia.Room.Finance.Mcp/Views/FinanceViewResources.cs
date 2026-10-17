using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Views;

/// <summary>
/// MCP Apps UI resources (ui://) served by the finance room.
/// </summary>
public static class FinanceViewResources
{
    public const string BalanceViewUri = "ui://finance/balance.html";

    // Every external domain used by a view must be declared, otherwise the host CSP blocks it.
    private static readonly McpUiResourceMeta ViewUi = new()
    {
        Csp = new McpUiResourceCsp { ResourceDomains = ["https://unpkg.com"] },
    };

    public static IEnumerable<McpServerResource> Create()
    {
        var balanceHtml = LoadView("balance.html");

        // McpApps.SetResourceUi only sets the template metadata, which is not exposed for a direct
        // (non-template) resource: the ui metadata is set explicitly, both in resources/list (Meta)
        // and in the resources/read contents.
        var balanceView = McpServerResource.Create(
            () => new TextResourceContents
            {
                Uri = BalanceViewUri,
                MimeType = McpApps.HtmlMimeType,
                Text = balanceHtml,
                Meta = UiMeta(),
            },
            new McpServerResourceCreateOptions
            {
                UriTemplate = BalanceViewUri,
                Name = "balance_view",
                Title = "Soldes des comptes",
                MimeType = McpApps.HtmlMimeType,
                Meta = UiMeta(),
            });

        return [balanceView];
    }

    private static JsonObject UiMeta() => new()
    {
        ["ui"] = JsonSerializer.SerializeToNode(ViewUi, McpApps.SerializerOptions),
    };

    private static string LoadView(string fileName)
    {
        var assembly = typeof(FinanceViewResources).Assembly;
        var resourceName = $"{typeof(FinanceViewResources).Namespace}.{fileName}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded view '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
