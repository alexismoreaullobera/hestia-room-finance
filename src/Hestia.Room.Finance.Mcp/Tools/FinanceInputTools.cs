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
/// data recalculated, so the view redraws from the server's figures without a second call. The tools of the plan view
/// are in <see cref="FinancePlanInputTools"/>.
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
    public static Task<CallToolResult> SetBudgetActual(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthArgument.RequiredDescription)] string month,
        [Description("Identifier of a budget of the month.")] Guid budgetId,
        [Description("Total really spent, in euros with at most two decimals (0 if nothing was spent), or null to go back to "
            + "the planned amount.")] decimal? actualAmount,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, FinanceDataTools.MonthSummaryAsync, ActualAmountRule("0 or more"), (financeMonth, yearMonth) =>
        {
            if (actualAmount is { } amount)
            {
                var actual = financeMonth.ActualFor(budgetId, amount);
                return () => writer.SetBudgetActualAsync(actual, cancellationToken);
            }

            financeMonth.EnsureBudget(budgetId);
            return () => writer.ClearBudgetActualAsync(budgetId, yearMonth, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "set_line_actual", Title = "Saisie : réel d'une ligne récurrente", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.MonthSummaryOutput))]
    [Description("Reserved for the month overview view, where the user types it: not to be called from a conversation. "
        + "Saves what happened to a recurring line due in a month: Corrected with its actualAmount (another amount), "
        + "Skipped (did not happen), or AsPlanned (back to the plan). Then returns the month summary recalculated, as "
        + "get_month_summary does.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> SetLineActual(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthArgument.RequiredDescription)] string month,
        [Description("Identifier of a recurring line due in the month.")] Guid lineId,
        [Description("AsPlanned, Corrected or Skipped.")] LineActualStatus status,
        [Description("The amount that really happened, in euros with at most two decimals: for Corrected only.")]
        decimal? actualAmount = null,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, FinanceDataTools.MonthSummaryAsync, ActualAmountRule("more than 0"), (financeMonth, yearMonth) =>
        {
            switch (status, actualAmount)
            {
                case (LineActualStatus.Corrected, { } amount):
                    var correction = financeMonth.CorrectionFor(lineId, amount);
                    return () => writer.SetLineOverrideAsync(correction, cancellationToken);
                case (LineActualStatus.Corrected, null):
                    throw new McpException("A Corrected line needs its actualAmount.");
                case (LineActualStatus.Skipped or LineActualStatus.AsPlanned, not null):
                    throw new McpException("actualAmount is for a Corrected line only.");
                case (LineActualStatus.Skipped, null):
                    var skipped = financeMonth.SkippedFor(lineId);
                    return () => writer.SetLineOverrideAsync(skipped, cancellationToken);
                case (LineActualStatus.AsPlanned, null):
                    financeMonth.EnsureLineDue(lineId);
                    return () => writer.ClearLineOverrideAsync(lineId, yearMonth, cancellationToken);
                default:
                    // A number outside the enum, which JSON can carry.
                    throw new McpException("Unknown status: expected AsPlanned, Corrected or Skipped.");
            }
        }, cancellationToken);

    /// <summary>
    /// The skeleton of the input tools of a month (the actuals): the month read, the domain deciding what to save, then
    /// the save, then the view data read back through the path of the data tool. Nothing is written when the domain
    /// refuses: its exception becomes a tool error, the decision never reaching the writer.
    /// </summary>
    /// <param name="readBack">The view data returned, as its data tool gives it.</param>
    /// <param name="valueRule">What a value of this tool must be, for the error of a value refused by the domain.</param>
    /// <param name="decide">The rules of the domain applied, giving the save to run.</param>
    private static async Task<CallToolResult> DecideSaveAndReadAsync(
        FinancePlanReader reader,
        string month,
        Func<FinancePlanReader, YearMonth, CancellationToken, Task<CallToolResult>> readBack,
        string valueRule,
        Func<FinanceMonth, YearMonth, Func<Task>> decide,
        CancellationToken cancellationToken)
    {
        var yearMonth = MonthArgument.Parse(month);
        var financeMonth = await reader.LoadMonthAsync(yearMonth, cancellationToken);

        Func<Task> save;
        try
        {
            save = decide(financeMonth, yearMonth);
        }
        catch (ArgumentException error)
        {
            throw Refused(error, yearMonth, valueRule);
        }

        await save();
        return await readBack(reader, yearMonth, cancellationToken);
    }

    private static string ActualAmountRule(string sign) => string.Create(CultureInfo.InvariantCulture,
        $"Invalid actualAmount: expected euros with at most two decimals, {sign}, at most {AmountLimit.Max}.");

    /// <summary>
    /// A rule of the domain broken by an input, as a tool error a client can read and fix: told apart by the type of the
    /// domain exception, never by a parameter name; never the value sent (the domain message and its details stay in the
    /// inner exception, for the server logs).
    /// </summary>
    private static McpException Refused(ArgumentException error, YearMonth month, string valueRule) => new(error switch
    {
        UnknownBudgetException => ToolErrors.UnknownBudget,
        LineNotDueException => $"No recurring line with this identifier is due in {month}.",
        _ => valueRule,
    }, error);
}
