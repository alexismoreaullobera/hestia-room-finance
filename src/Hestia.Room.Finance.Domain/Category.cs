namespace Hestia.Room.Finance.Domain;

/// <summary>
/// Fixed list of categories, so that views by category stay reliable.
/// User-defined categories may replace it later (see docs/scope.md).
/// </summary>
public enum Category
{
    Other,
    Income,
    Housing,
    Utilities,
    Telecom,
    Insurance,
    Transport,
    Health,
    Food,
    Leisure,
    Education,
    Savings,
    Taxes,
}
