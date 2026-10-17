using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using Hestia.Room.Finance.Mcp.Data;
using Hestia.Room.Finance.Mcp.Views;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

[McpServerToolType]
public class FinanceViewTools
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly JsonSerializerOptions WidgetJson = new(JsonSerializerDefaults.Web);

    [McpServerTool(Name = "show_balance", ReadOnly = true)]
    [Description("Shows the balance of each account and the total balance.")]
    [McpAppUi(ResourceUri = FinanceViewResources.BalanceViewUri)]
    public static CallToolResult ShowBalance()
    {
        var accounts = FakeAccounts.All;
        var total = accounts.Sum(a => a.Balance);
        var details = string.Join(", ", accounts.Select(a => $"{a.Name} : {a.Balance.ToString("C", French)}"));

        return new CallToolResult
        {
            // Self-sufficient text summary: used by the model and by hosts without UI (voice-only mode).
            Content = [new TextContentBlock { Text = $"Solde total : {total.ToString("C", French)}. {details}." }],
            // Structured data for the widget: no text parsing on the view side.
            StructuredContent = JsonSerializer.SerializeToElement(new BalanceView(accounts, total), WidgetJson),
        };
    }

    private sealed record BalanceView(IReadOnlyList<Account> Accounts, decimal Total);
}
