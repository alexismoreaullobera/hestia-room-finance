namespace Hestia.Room.Finance.Domain;

/// <summary>The bound of every amount the room accepts, public so that the inputs can state it in their messages.</summary>
public static class AmountLimit
{
    /// <summary>
    /// The largest amount, in euros, either way: far beyond any household figure, and far below what the database
    /// stores (decimal(18,2)), so that no sum of the month can overflow it.
    /// </summary>
    public const decimal Max = 999_999_999.99m;
}
