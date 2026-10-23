using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Views;

/// <summary>
/// MCP Apps UI resources (ui://) served by the finance room: one HTML view per view tool.
/// </summary>
public static class FinanceViewResources
{
    public const string MonthOverviewUri = "ui://finance/month-overview.html";
    public const string CashFlowForecastUri = "ui://finance/cash-flow-forecast.html";
    public const string SubscriptionsUri = "ui://finance/subscriptions.html";

    /// <summary>Where a view takes the code shared by every view (styles, then script), replaced at startup.</summary>
    internal const string SharedStylesMarker = "/* @shared view.css */";
    internal const string SharedScriptMarker = "/* @shared view.js */";

    private static readonly ViewDefinition[] Views =
    [
        new(MonthOverviewUri, "month_overview_view", "Synthèse du mois", "month-overview.html"),
        new(CashFlowForecastUri, "cash_flow_forecast_view", "Prévisionnel de trésorerie", "cash-flow-forecast.html"),
        new(SubscriptionsUri, "subscriptions_view", "Abonnements", "subscriptions.html"),
    ];

    // Every external domain used by a view must be declared, otherwise the host CSP blocks it.
    private static readonly McpUiResourceMeta ViewUi = new()
    {
        Csp = new McpUiResourceCsp { ResourceDomains = ["https://unpkg.com"] },
    };

    public static IEnumerable<McpServerResource> Create()
    {
        var styles = LoadView("shared/view.css");
        var script = LoadView("shared/view.js");

        return [.. Views.Select(view => CreateResource(view, Compose(LoadView(view.FileName), styles, script)))];
    }

    /// <summary>
    /// The view with the shared code in place of its markers: each view stays a single self-contained HTML document,
    /// as MCP Apps expects, without copying the shared code into every file.
    /// </summary>
    internal static string Compose(string html, string styles, string script)
    {
        if (!html.Contains(SharedStylesMarker, StringComparison.Ordinal) || !html.Contains(SharedScriptMarker, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("A view must contain both shared code markers.");
        }

        return html.Replace(SharedStylesMarker, styles, StringComparison.Ordinal).Replace(SharedScriptMarker, script, StringComparison.Ordinal);
    }

    private static McpServerResource CreateResource(ViewDefinition view, string html) =>
        // McpApps.SetResourceUi only sets the template metadata, which is not exposed for a direct
        // (non-template) resource: the ui metadata is set explicitly, both in resources/list (Meta)
        // and in the resources/read contents.
        McpServerResource.Create(
            () => new TextResourceContents
            {
                Uri = view.Uri,
                MimeType = McpApps.HtmlMimeType,
                Text = html,
                Meta = UiMeta(),
            },
            new McpServerResourceCreateOptions
            {
                UriTemplate = view.Uri,
                Name = view.Name,
                Title = view.Title,
                MimeType = McpApps.HtmlMimeType,
                Meta = UiMeta(),
            });

    private static JsonObject UiMeta() => new()
    {
        ["ui"] = JsonSerializer.SerializeToNode(ViewUi, McpApps.SerializerOptions),
    };

    private static string LoadView(string path)
    {
        var assembly = typeof(FinanceViewResources).Assembly;
        // Embedded resource names use dots for folders: Views/shared/view.css -> ...Views.shared.view.css.
        var resourceName = $"{typeof(FinanceViewResources).Namespace}.{path.Replace('/', '.')}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded view '{resourceName}' not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record ViewDefinition(string Uri, string Name, string Title, string FileName);
}
