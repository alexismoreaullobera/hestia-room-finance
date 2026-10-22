using System.Runtime.CompilerServices;

namespace Hestia.Room.Finance.Domain;

internal static class Guard
{
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
}
