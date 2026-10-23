using System.Runtime.CompilerServices;

namespace Hestia.Room.Finance.Domain;

internal static class Guard
{
    /// <summary>
    /// The largest amount the room accepts, in euros, either way: far beyond any household figure, and far below what
    /// the database stores (decimal(18,2)), so that no sum of the month can overflow it.
    /// </summary>
    public const decimal MaxAmount = 999_999_999.99m;

    /// <summary>
    /// Rejects an empty identifier: Guid.Empty is what an uninitialized Guid holds, never a real identity.
    /// </summary>
    public static void ThrowIfEmpty(Guid value, [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("An identifier cannot be empty.", paramName);
        }
    }

    /// <summary>
    /// Rejects an amount with more than two decimals: a bank account knows no fraction of a cent.
    /// </summary>
    public static void ThrowIfNotCents(decimal amount, [CallerArgumentExpression(nameof(amount))] string? paramName = null)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException("An amount in euros has at most two decimals.", paramName);
        }
    }

    /// <summary>
    /// Rejects an amount beyond <see cref="MaxAmount"/>, positive or negative: what an input sent by hand (a typo, a
    /// call outside the views) could hold, never a real figure of a month.
    /// </summary>
    public static void ThrowIfTooLarge(decimal amount, [CallerArgumentExpression(nameof(amount))] string? paramName = null)
    {
        if (Math.Abs(amount) > MaxAmount)
        {
            throw new ArgumentOutOfRangeException(paramName, amount, $"An amount in euros is at most {MaxAmount} either way.");
        }
    }
}
