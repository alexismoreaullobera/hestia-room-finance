using System.Globalization;
using System.Text;
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
    /// A name the user typed: in its composed Unicode form (NFC: an accented letter typed as one character or as a letter
    /// and a combining accent is the same name), trimmed, not empty, at most <see cref="NameLimit.MaxLength"/>
    /// characters, with at least one visible character, and none of the characters that would break a line or hide text
    /// when the name is read back, by a person or by the model.
    /// </summary>
    /// <summary>
    /// Two names of the plan read as the same one: ignoring case ("Courses" and "courses"), ordinal, never the culture of
    /// the server. Accents count: "Épargne" and "Epargne" are two names.
    /// </summary>
    public static bool IsSameName(string name, string other) => string.Equals(name, other, StringComparison.OrdinalIgnoreCase);

    public static string ValidName(string name, [CallerArgumentExpression(nameof(name))] string? paramName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name, paramName);
        string composed;
        try
        {
            composed = name.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException error)
        {
            // An unpaired surrogate: no character at all.
            throw new ArgumentException("A name is made of valid characters.", paramName, error);
        }

        var trimmed = composed.Trim();
        if (trimmed.Length > NameLimit.MaxLength)
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"A name has at most {NameLimit.MaxLength} characters."), paramName);
        }

        if (trimmed.EnumerateRunes().Any(IsForbiddenInAName))
        {
            throw new ArgumentException("A name has no control, invisible nor direction character, and no line break.", paramName);
        }

        if (!trimmed.EnumerateRunes().Any(IsVisible))
        {
            throw new ArgumentException("A name has at least one visible character.", paramName);
        }

        return trimmed;
    }

    /// <summary>
    /// Refused in a name, by Unicode category, never by a list of characters written in the source:
    /// <list type="bullet">
    /// <item>controls and line or paragraph separators, which break a line;</item>
    /// <item>format characters, invisible: zero-width spaces and joiners, the byte order mark, the soft hyphen, the
    /// direction marks, embeddings, overrides and isolates (which reorder what is shown), and the tag characters (which
    /// carry text a person never sees but a model reads);</item>
    /// <item>private use, unassigned and surrogate code points, and the replacement character an unpaired one becomes;</item>
    /// <item>the fillers that show as blank although they are letters or symbols (Hangul fillers, the blank Braille pattern).</item>
    /// </list>
    /// </summary>
    private static bool IsForbiddenInAName(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.Control or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator or UnicodeCategory.PrivateUse
            or UnicodeCategory.OtherNotAssigned or UnicodeCategory.Surrogate
        || rune == Rune.ReplacementChar
        || rune.Value is 0x115F or 0x1160 or 0x3164 or 0xFFA0 or 0x2800;

    /// <summary>A character a person sees: a letter, a digit, a punctuation mark or a symbol (not a space nor an accent alone).</summary>
    private static bool IsVisible(Rune rune) =>
        Rune.GetUnicodeCategory(rune) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter
            or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber
            or UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation or UnicodeCategory.OpenPunctuation
            or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
            or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation or UnicodeCategory.MathSymbol
            or UnicodeCategory.CurrencySymbol or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;

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
