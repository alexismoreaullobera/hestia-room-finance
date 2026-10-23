namespace Hestia.Room.Finance.Mcp.Tools.Outputs;

/// <summary>The rounding policy of every tool output, structured content and text alike.</summary>
internal static class Amounts
{
    /// <summary>
    /// The domain keeps exact amounts (a 100 € quarterly line weighs 33.333… € a month); outputs show cents.
    /// A half cent goes away from zero, as people round money (2.505 € shows 2.51 €, not the banker's 2.50 €).
    /// </summary>
    public static decimal Cents(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);
}
