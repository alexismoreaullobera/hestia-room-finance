using System.Globalization;
using Hestia.Room.Finance.Mcp.Tools;
using ModelContextProtocol;

namespace Hestia.Room.Finance.Mcp.Tests.Tools;

public class FinanceInputToolsTests
{
    [Fact]
    public async Task SetStartingBalance_StatesTheBoundInvariantly_WhateverTheCultureOfTheServer()
    {
        // On a French server, the current culture would write 999999999,99: the message must not depend on it. Called
        // directly, since the CI runs in another culture: the domain refuses the value before the writer or the reader
        // is used, hence no services.
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
        try
        {
            var error = await Assert.ThrowsAsync<McpException>(() =>
                FinanceInputTools.SetStartingBalance(null!, null!, "2031-02", 1_000_000_000m, TestContext.Current.CancellationToken));

            Assert.Contains("999999999.99", error.Message);
            // The domain exception kept for the server logs, not in the message the client reads.
            Assert.IsType<ArgumentOutOfRangeException>(error.InnerException);
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }
}
