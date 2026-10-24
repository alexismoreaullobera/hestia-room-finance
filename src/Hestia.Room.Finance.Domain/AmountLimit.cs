namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The bound of the amounts the user types, public so that the inputs can state it in their messages. Applied to the
/// starting balance, the budget actuals and the corrections of recurring lines; the amounts of the plan itself (lines,
/// budgets) get it when they become inputs (T11).
/// </summary>
public static class AmountLimit
{
    /// <summary>
    /// The largest amount, in euros, either way: far beyond any household figure, and far below what the database
    /// stores (decimal(18,2)).
    /// </summary>
    public const decimal Max = 999_999_999.99m;
}
