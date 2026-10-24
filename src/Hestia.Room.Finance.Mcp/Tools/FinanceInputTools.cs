using System.ComponentModel;
using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// Input tools: the writes of the user, called by the views. They are declared with the MCP Apps visibility "app":
/// an MCP Apps host hides them from the model and lets the views call them, but a host without MCP Apps support ignores
/// that metadata and offers them to its model like any other tool. Not access control either: the server does not know
/// who calls (see issue #2). Every value goes through the domain before being saved, and each tool returns the view
/// data recalculated, so the view redraws from the server's figures without a second call.
/// </summary>
[McpServerToolType]
public sealed class FinanceInputTools
{
    [McpServerTool(Name = "set_starting_balance", Title = "Saisie : solde de départ du mois", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.CashFlowForecastOutput))]
    [Description("Reserved for the cash flow forecast view, where the user types it: not to be called from a conversation. "
        + "Saves the starting balance of a month (negative if the account is already overdrawn), then returns its cash "
        + "flow forecast recalculated, as get_cash_flow_forecast does.")]
    // No resource URI: called from a view already shown, it must not make the host open another one.
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static async Task<CallToolResult> SetStartingBalance(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthArgument.RequiredDescription)] string month,
        [Description("Balance of the account at the start of the month, in euros with at most two decimals.")] decimal startingBalance,
        CancellationToken cancellationToken = default)
    {
        var yearMonth = MonthArgument.Parse(month);
        MonthPlan plan;
        try
        {
            plan = new MonthPlan(yearMonth, startingBalance);
        }
        catch (ArgumentException error)
        {
            // Both rules of the domain in one message: a tool error (isError) a client can read and fix. The domain
            // exception is kept as the inner one for the server logs; only this message reaches the client.
            // Invariant: the same message (999999999.99) whatever the culture of the server.
            throw new McpException(string.Create(CultureInfo.InvariantCulture,
                $"Invalid starting balance: expected euros with at most two decimals, at most {AmountLimit.Max} either way."), error);
        }

        await writer.SetStartingBalanceAsync(plan, cancellationToken);
        // Read back from the database, through the same path as the data tool: what the view shows is what was saved.
        return await FinanceDataTools.CashFlowForecastAsync(reader, yearMonth, cancellationToken);
    }

    [McpServerTool(Name = "set_budget_actual", Title = "Saisie : réel d'un budget", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.MonthSummaryOutput))]
    [Description("Reserved for the month overview view, where the user types it: not to be called from a conversation. "
        + "Saves what was really spent on a budget during a month, or, with actualAmount null, goes back to the planned "
        + "amount. Then returns the month summary recalculated, as get_month_summary does.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static async Task<CallToolResult> SetBudgetActual(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthArgument.RequiredDescription)] string month,
        [Description("Identifier of a budget of the month.")] Guid budgetId,
        [Description("Total really spent, in euros with at most two decimals (0 if nothing was spent), or null to go back to "
            + "the planned amount.")] decimal? actualAmount,
        CancellationToken cancellationToken = default)
    {
        var yearMonth = MonthArgument.Parse(month);
        var financeMonth = await reader.LoadMonthAsync(yearMonth, cancellationToken);

        // The domain decides first; nothing is written when it refuses.
        Func<Task> save;
        try
        {
            if (actualAmount is { } amount)
            {
                var actual = financeMonth.ActualFor(budgetId, amount);
                save = () => writer.SetBudgetActualAsync(actual, cancellationToken);
            }
            else
            {
                financeMonth.EnsureBudget(budgetId);
                save = () => writer.ClearBudgetActualAsync(budgetId, yearMonth, cancellationToken);
            }
        }
        catch (ArgumentException error)
        {
            throw Refused(error, yearMonth, amountRule: "0 or more");
        }

        await save();
        return await FinanceDataTools.MonthSummaryAsync(reader, yearMonth, cancellationToken);
    }

    [McpServerTool(Name = "set_line_actual", Title = "Saisie : réel d'une ligne récurrente", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.MonthSummaryOutput))]
    [Description("Reserved for the month overview view, where the user types it: not to be called from a conversation. "
        + "Saves what happened to a recurring line due in a month: Corrected with its actualAmount (another amount), "
        + "Skipped (did not happen), or AsPlanned (back to the plan). Then returns the month summary recalculated, as "
        + "get_month_summary does.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static async Task<CallToolResult> SetLineActual(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthArgument.RequiredDescription)] string month,
        [Description("Identifier of a recurring line due in the month.")] Guid lineId,
        [Description("AsPlanned, Corrected or Skipped.")] LineActualStatus status,
        [Description("The amount that really happened, in euros with at most two decimals: for Corrected only.")]
        decimal? actualAmount = null,
        CancellationToken cancellationToken = default)
    {
        var yearMonth = MonthArgument.Parse(month);
        var financeMonth = await reader.LoadMonthAsync(yearMonth, cancellationToken);

        // The domain decides first; nothing is written when it refuses.
        Func<Task> save;
        try
        {
            switch (status, actualAmount)
            {
                case (LineActualStatus.Corrected, { } amount):
                    var correction = financeMonth.CorrectionFor(lineId, amount);
                    save = () => writer.SetLineOverrideAsync(correction, cancellationToken);
                    break;
                case (LineActualStatus.Corrected, null):
                    throw new McpException("A Corrected line needs its actualAmount.");
                case (LineActualStatus.Skipped or LineActualStatus.AsPlanned, not null):
                    throw new McpException("actualAmount is for a Corrected line only.");
                case (LineActualStatus.Skipped, null):
                    var skipped = financeMonth.SkippedFor(lineId);
                    save = () => writer.SetLineOverrideAsync(skipped, cancellationToken);
                    break;
                case (LineActualStatus.AsPlanned, null):
                    financeMonth.EnsureLineDue(lineId);
                    save = () => writer.ClearLineOverrideAsync(lineId, yearMonth, cancellationToken);
                    break;
                default:
                    // A number outside the enum, which JSON can carry.
                    throw new McpException("Unknown status: expected AsPlanned, Corrected or Skipped.");
            }
        }
        catch (ArgumentException error)
        {
            throw Refused(error, yearMonth, amountRule: "more than 0");
        }

        await save();
        return await FinanceDataTools.MonthSummaryAsync(reader, yearMonth, cancellationToken);
    }

    /// <summary>
    /// A rule of the domain broken by an actual, as a tool error a client can read and fix: which argument, never its
    /// value (the domain message and its details stay in the inner exception, for the server logs).
    /// </summary>
    private static McpException Refused(ArgumentException error, YearMonth month, string amountRule) => new(error.ParamName switch
    {
        "budgetId" => $"No budget with this identifier in {month}.",
        "lineId" => $"No recurring line with this identifier is due in {month}.",
        _ => string.Create(CultureInfo.InvariantCulture,
            $"Invalid actualAmount: expected euros with at most two decimals, {amountRule}, at most {AmountLimit.Max}."),
    }, error);
}
