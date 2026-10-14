using System.ComponentModel;
using System.Globalization;
using Hestia.Room.Finance.Mcp.Data;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

[McpServerToolType]
public class FinanceViewTools
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [McpServerTool(Name = "show_balance")]
    [Description("Shows the balance of each account and the total balance.")]
    [McpAppUi(ResourceUri = "ui://finance/balance.html")]
    public static string ShowBalance()
    {
        var accounts = FakeAccounts.All;
        var total = accounts.Sum(a => a.Balance);
        var details = string.Join(", ", accounts.Select(a => $"{a.Name} : {a.Balance.ToString("C", French)}"));

        // Self-sufficient text summary: used by the model and by hosts without UI (voice-only mode).
        return $"Solde total : {total.ToString("C", French)}. {details}.";
    }
}
