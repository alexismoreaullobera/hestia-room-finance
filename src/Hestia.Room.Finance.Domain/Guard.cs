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
}
