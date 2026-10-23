using Hestia.Room.Finance.Mcp.Views;

namespace Hestia.Room.Finance.Mcp.Tests.Views;

/// <summary>The composition of a view with the shared code, without a server: fast to run and to diagnose.</summary>
public class ViewCompositionTests
{
    private const string Styles = "/* @shared view.css */";
    private const string Script = "/* @shared view.js */";

    [Fact]
    public void Compose_PutsTheSharedStylesAndScriptInPlaceOfTheirMarkers()
    {
        var html = $"<style>{Styles} .local {{}}</style><script type=\"module\">{Script} startView();</script>";

        var composed = FinanceViewResources.Compose("view.html", html, "SHARED-CSS", "SHARED-JS");

        Assert.Equal("<style>SHARED-CSS .local {}</style><script type=\"module\">SHARED-JS startView();</script>", composed);
    }

    [Theory]
    [InlineData($"<style></style><script>{Script}</script>")]
    [InlineData($"<style>{Styles}</style><script></script>")]
    [InlineData($"<style>{Styles}{Styles}</style><script>{Script}</script>")]
    [InlineData($"<style>{Styles}</style><script>{Script}{Script}</script>")]
    public void Compose_WithAMissingOrDuplicatedMarker_NamesTheView(string html)
    {
        var error = Assert.Throws<InvalidOperationException>(() => FinanceViewResources.Compose("broken.html", html, "css", "js"));

        Assert.Contains("broken.html", error.Message);
        Assert.Contains("exactly once", error.Message);
    }
}
