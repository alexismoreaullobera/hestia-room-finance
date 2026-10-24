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
}
