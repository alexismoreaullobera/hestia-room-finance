using System.Globalization;
using Hestia.Room.Finance.Mcp.Data;
using Hestia.Room.Finance.Mcp.Tools;
using ModelContextProtocol.Protocol;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class FinanceViewToolsTests
{
    private static readonly CultureInfo French = CultureInfo.GetCultureInfo("fr-FR");

    [Fact]
    public void ShowBalance_ReturnsOneLinePerAccountAndTheirSumAsTotal()
    {
        var result = FinanceViewTools.ShowBalance();

        var view = result.StructuredContent!.Value;
        var accounts = view.GetProperty("accounts").EnumerateArray().ToList();
        var balances = accounts.Select(a => a.GetProperty("balance").GetDecimal()).ToList();

        Assert.Equal(FakeAccounts.All.Count, accounts.Count);
        Assert.Equal(balances.Sum(), view.GetProperty("total").GetDecimal());
    }

    [Fact]
    public void ShowBalance_TextSummaryIsSelfSufficient()
    {
        var expectedTotal = FakeAccounts.All.Sum(a => a.Balance).ToString("C", French);

        var result = FinanceViewTools.ShowBalance();

        var text = Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text;
        Assert.Contains(expectedTotal, text);
        Assert.All(FakeAccounts.All, account => Assert.Contains(account.Name, text));
    }

    [Fact]
    public void ShowBalance_IsNotAnError()
    {
        var result = FinanceViewTools.ShowBalance();

        Assert.False(result.IsError ?? false);
    }
}
