using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Views;

/// <summary>
/// MCP Apps UI resources (ui://) served by the finance room: one HTML view per view tool, built by the views project
/// (src/Hestia.Room.Finance.Views) as a self-contained document (scripts, styles and App SDK inlined) and embedded in
/// this assembly.
/// </summary>
public static class FinanceViewResources
{
    public const string MonthOverviewUri = "ui://finance/month-overview.html";
    public const string CashFlowForecastUri = "ui://finance/cash-flow-forecast.html";
    public const string SubscriptionsUri = "ui://finance/subscriptions.html";
    public const string PlanUri = "ui://finance/plan.html";

    private static readonly ViewDefinition[] Views =
    [
        new(MonthOverviewUri, "month_overview_view", "Synthèse du mois", "month-overview.html",
            "View of show_month_overview: planned and actual savings capacity, incomes, recurring expenses and budgets."),
        new(CashFlowForecastUri, "cash_flow_forecast_view", "Prévisionnel de trésorerie", "cash-flow-forecast.html",
            "View of show_cash_flow_forecast: alert, low point, cash buffer, daily balance chart and contributing debits."),
        new(SubscriptionsUri, "subscriptions_view", "Abonnements", "subscriptions.html",
            "View of show_subscriptions: monthly and yearly totals, debits of the month and commitment ends."),
        new(PlanUri, "plan_view", "Plan", "plan.html",
            "View of show_plan: the budgets of the plan, created, edited and deleted there."),
    ];

    // No external domain: the views are self-contained, the App SDK included. With an empty CSP, the host applies the
    // MCP Apps default: no fetch, XHR or WebSocket, no external script, style or image. The navigation of the frame
    // and the requests to the host (open a link, send a message) are governed by the host, not by this CSP.
    private static readonly McpUiResourceMeta ViewUi = new()
    {
        Csp = new McpUiResourceCsp(),
    };

    public static IEnumerable<McpServerResource> Create() =>
        [.. Views.Select(view => CreateResource(view, LoadView(view.FileName)))];

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
                Description = view.Description,
                MimeType = McpApps.HtmlMimeType,
                Meta = UiMeta(),
            });

    private static JsonObject UiMeta() => new()
    {
        // The SDK options (its source-generated context, McpAppsJsonContext, is internal in 2.3.0).
        ["ui"] = JsonSerializer.SerializeToNode(ViewUi, McpApps.SerializerOptions),
    };

    private static string LoadView(string path)
    {
        // The LogicalName given in the csproj: views/month-overview.html.
        var resourceName = $"views/{path}";

        using var stream = typeof(FinanceViewResources).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded view '{resourceName}' not found: build the views project (dotnet build does it).");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record ViewDefinition(string Uri, string Name, string Title, string FileName, string Description);
}
