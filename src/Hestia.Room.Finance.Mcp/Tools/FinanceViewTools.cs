using System.ComponentModel;
using Hestia.Room.Finance.Infrastructure;
using Hestia.Room.Finance.Mcp.Time;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using Hestia.Room.Finance.Mcp.Views;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// View tools: each one is its data tool plus a <c>ui://</c> view, which MCP Apps hosts render with the result.
/// Same result (text summary, structured content, output schema), so the view and the agent see the same figures;
/// a host without UI simply shows the text. Called by the agent to show a view, and by the views themselves to
/// change month.
/// </summary>
[McpServerToolType]
public sealed class FinanceViewTools
{
    [McpServerTool(Name = "show_month_overview", Title = "Vue : synthèse du mois", ReadOnly = true, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(MonthSummaryOutput))]
    [Description("Shows the user the month overview: planned and actual incomes, recurring expenses, budgets and savings "
        + "capacity. Same data as get_month_summary, displayed as a view.")]
    [McpAppUi(ResourceUri = FinanceViewResources.MonthOverviewUri)]
    public static Task<CallToolResult> ShowMonthOverview(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        FinanceDataTools.GetMonthSummary(reader, clock, month, cancellationToken);

    [McpServerTool(Name = "show_cash_flow_forecast", Title = "Vue : prévisionnel de trésorerie", ReadOnly = true,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(CashFlowForecastOutput))]
    [Description("Shows the user the day-by-day cash flow forecast as a chart: low point, cash buffer, alert and the "
        + "debits that dig the low point. Same data as get_cash_flow_forecast, displayed as a view.")]
    [McpAppUi(ResourceUri = FinanceViewResources.CashFlowForecastUri)]
    public static Task<CallToolResult> ShowCashFlowForecast(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        FinanceDataTools.GetCashFlowForecast(reader, clock, month, cancellationToken);

    [McpServerTool(Name = "show_subscriptions", Title = "Vue : abonnements", ReadOnly = true, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(SubscriptionsOutput))]
    [Description("Shows the user the subscriptions with their monthly and yearly cost, debit dates and commitment ends. "
        + "Same data as list_subscriptions, displayed as a view.")]
    [McpAppUi(ResourceUri = FinanceViewResources.SubscriptionsUri)]
    public static Task<CallToolResult> ShowSubscriptions(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        FinanceDataTools.ListSubscriptions(reader, clock, month, cancellationToken);

    [McpServerTool(Name = "show_plan", Title = "Vue : plan", ReadOnly = true, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true, OutputSchemaType = typeof(PlanOutput))]
    [Description("Shows the user the plan itself, where it is edited: the budgets with their planned amount. Same data as "
        + "get_plan, displayed as a view.")]
    [McpAppUi(ResourceUri = FinanceViewResources.PlanUri)]
    public static Task<CallToolResult> ShowPlan(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        FinanceDataTools.GetPlan(reader, clock, month, cancellationToken);
}
