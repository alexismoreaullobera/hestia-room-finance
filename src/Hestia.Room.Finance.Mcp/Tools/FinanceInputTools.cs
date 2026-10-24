using System.ComponentModel;
using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure;
using Hestia.Room.Finance.Mcp.Time;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// Input tools: the writes of the user, called by the views (MCP Apps visibility "app"), never offered to the model.
/// The visibility only asks the hosts to hide them from the model: it is not access control, the server does not know
/// who calls (see issue #2). Every value goes through the domain before being saved, and each tool returns the view
/// data recalculated, so the view redraws from the server's figures without a second call.
/// </summary>
[McpServerToolType]
public sealed class FinanceInputTools
{
    [McpServerTool(Name = "set_starting_balance", Title = "Saisie : solde de départ du mois", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.CashFlowForecastOutput))]
    [Description("Saves the starting balance of a month (negative if the account is already overdrawn), then returns its "
        + "cash flow forecast recalculated, as get_cash_flow_forecast does. Called by the cash flow forecast view.")]
    // No resource URI: called from a view already shown, it must not make the host open another one.
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static async Task<CallToolResult> SetStartingBalance(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        FinanceClock clock,
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
        catch (ArgumentException)
        {
            // The rules of the domain, stated once: a tool error (isError) a client can read and fix, without the
            // exception details (parameter name, value) of the domain message.
            // Invariant: the same message (999999999.99) whatever the culture of the server.
            throw new McpException(string.Create(CultureInfo.InvariantCulture,
                $"Invalid starting balance: expected euros with at most two decimals, at most {AmountLimit.Max} either way."));
        }

        await writer.SetStartingBalanceAsync(plan, cancellationToken);
        // Read back from the database, through the same path as the data tool: what the view shows is what was saved.
        return await FinanceDataTools.GetCashFlowForecast(reader, clock, yearMonth.ToString(), cancellationToken);
    }
}
