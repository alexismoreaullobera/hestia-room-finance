using System.ComponentModel;
using System.Text.Json;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure;
using Hestia.Room.Finance.Mcp.Time;
using Hestia.Room.Finance.Mcp.Tools.Outputs;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// Read-only data tools, called by the agent. Each tool loads one month, then projects it: a structured output
/// (with its advertised schema) and a self-sufficient text summary. The calculations are done by the domain, never
/// by the model. The reader and the clock come from dependency injection: the reader is scoped (one per HTTP request,
/// with its DbContext), the clock is a singleton.
/// </summary>
[McpServerToolType]
public sealed class FinanceDataTools
{
    [McpServerTool(Name = "get_month_summary", Title = "Synthèse du mois", ReadOnly = true, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true, OutputSchemaType = typeof(MonthSummaryOutput))]
    [Description("Planned and actual summary of a month: incomes, recurring expenses, budgets and savings capacity, "
        + "as falling due this month and smoothed over the year.")]
    public static Task<CallToolResult> GetMonthSummary(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        MonthSummaryAsync(reader, MonthArgument.Resolve(month, clock), cancellationToken);

    /// <summary>The result of get_month_summary for a month already resolved: also what an input tool returns.</summary>
    internal static async Task<CallToolResult> MonthSummaryAsync(
        FinancePlanReader reader, YearMonth month, CancellationToken cancellationToken)
    {
        var financeMonth = await reader.LoadMonthAsync(month, cancellationToken);
        var output = MonthSummaryOutput.From(financeMonth);
        return Result(output, output.Describe());
    }

    [McpServerTool(Name = "get_cash_flow_forecast", Title = "Prévisionnel de trésorerie", ReadOnly = true, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(CashFlowForecastOutput))]
    [Description("Day-by-day cash flow forecast of a month from the starting balance: low point, cash buffer, alert when "
        + "the balance goes below zero, and the recurring expenses that dig the low point.")]
    public static Task<CallToolResult> GetCashFlowForecast(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        CashFlowForecastAsync(reader, MonthArgument.Resolve(month, clock), cancellationToken);

    /// <summary>The result of get_cash_flow_forecast for a month already resolved: also what an input tool returns.</summary>
    internal static async Task<CallToolResult> CashFlowForecastAsync(
        FinancePlanReader reader, YearMonth month, CancellationToken cancellationToken)
    {
        var financeMonth = await reader.LoadMonthAsync(month, cancellationToken);
        var output = CashFlowForecastOutput.From(financeMonth.Forecast());
        return Result(output, output.Describe());
    }

    [McpServerTool(Name = "list_recurring_lines", Title = "Lignes récurrentes", ReadOnly = true, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(RecurringLinesOutput))]
    [Description("Recurring incomes and expenses running during a month (salary, rent, energy, insurance, subscriptions), "
        + "with their date in the month and their monthly equivalent.")]
    public static async Task<CallToolResult> ListRecurringLines(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default)
    {
        var financeMonth = await reader.LoadMonthAsync(MonthArgument.Resolve(month, clock), cancellationToken);
        var output = RecurringLinesOutput.From(financeMonth);
        return Result(output, output.Describe());
    }

    [McpServerTool(Name = "list_subscriptions", Title = "Abonnements", ReadOnly = true, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true, OutputSchemaType = typeof(SubscriptionsOutput))]
    [Description("Subscriptions running during a month, with their monthly and yearly cost, their debit date in the month "
        + "and the end of their commitment, plus the monthly and yearly totals.")]
    public static async Task<CallToolResult> ListSubscriptions(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default)
    {
        var financeMonth = await reader.LoadMonthAsync(MonthArgument.Resolve(month, clock), cancellationToken);
        var output = SubscriptionsOutput.From(financeMonth);
        return Result(output, output.Describe());
    }

    [McpServerTool(Name = "get_plan", Title = "Plan", ReadOnly = true, Idempotent = true, OpenWorld = false,
        UseStructuredContent = true, OutputSchemaType = typeof(PlanOutput))]
    [Description("The plan itself as it stands for a month: the budgets (variable expenses planned every month), with "
        + "their planned amount and the number of months an actual was entered for; and the recurring lines running during "
        + "the month (incomes, fixed expenses, subscriptions), with every field and the number of months a correction was "
        + "entered for.")]
    public static Task<CallToolResult> GetPlan(
        FinancePlanReader reader,
        FinanceClock clock,
        [Description(MonthArgument.Description)] string? month = null,
        CancellationToken cancellationToken = default) =>
        PlanAsync(reader, MonthArgument.Resolve(month, clock), cancellationToken);

    /// <summary>The result of get_plan for a month already resolved: also what an input tool of the plan returns.</summary>
    internal static async Task<CallToolResult> PlanAsync(FinancePlanReader reader, YearMonth month, CancellationToken cancellationToken)
    {
        var financeMonth = await reader.LoadMonthAsync(month, cancellationToken);
        var actualsByBudget = await reader.CountActualsByBudgetAsync(cancellationToken);
        var overridesByLine = await reader.CountOverridesByLineAsync(cancellationToken);
        var output = PlanOutput.From(financeMonth, actualsByBudget, overridesByLine);
        return Result(output, output.Describe());
    }

    private static CallToolResult Result<T>(T output, string summary) => new()
    {
        // Text for the model and for hosts without UI; structured content matching the advertised output schema,
        // serialized with the SDK options that also generated that schema.
        Content = [new TextContentBlock { Text = summary }],
        StructuredContent = JsonSerializer.SerializeToElement(output, McpJsonUtilities.DefaultOptions),
    };
}
