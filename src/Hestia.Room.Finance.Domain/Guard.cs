using System.Globalization;
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

    /// <summary>
    /// A name the user typed, trimmed: not empty, at most <see cref="NameLimit.MaxLength"/> characters, and none of the
    /// characters that would break a line or hide text when the name is read back, by a person or by the model (control
    /// characters, line and paragraph separators, bidirectional overrides).
    /// </summary>
    public static string ValidName(string name, [CallerArgumentExpression(nameof(name))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, paramName);
        var trimmed = name.Trim();
        if (trimmed.Length > NameLimit.MaxLength)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"A name has at most {NameLimit.MaxLength} characters."), paramName);
        }

        if (trimmed.Any(IsForbiddenInAName))
        {
            throw new ArgumentException("A name has no control character, line break nor direction override.", paramName);
        }

        return trimmed;
    }

    private static bool IsForbiddenInAName(char character) =>
        char.IsControl(character)
        || char.GetUnicodeCategory(character) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
        // Bidirectional embeddings, overrides and isolates: they reorder what is shown, a name could hide its real text.
        || character is (>= (char)0x202A and <= (char)0x202E) or (>= (char)0x2066 and <= (char)0x2069);

    /// <summary>
    /// Rejects an amount beyond <see cref="AmountLimit.Max"/>, positive or negative: what an input sent by hand (a
    /// typo, a call outside the views) could hold, never a real figure of a month.
    /// </summary>
    public static void ThrowIfTooLarge(decimal amount, [CallerArgumentExpression(nameof(amount))] string? paramName = null)
    {
        if (Math.Abs(amount) > AmountLimit.Max)
        {
            // Invariant: the same message whatever the culture, in the logs too (a row read back goes through here).
            throw new ArgumentOutOfRangeException(paramName, amount,
                string.Create(CultureInfo.InvariantCulture, $"An amount in euros is at most {AmountLimit.Max} either way."));
        }
    }
}
