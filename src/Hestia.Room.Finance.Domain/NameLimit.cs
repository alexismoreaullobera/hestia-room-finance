namespace Hestia.Room.Finance.Domain;

/// <summary>The rule of the names the user types (budgets; recurring lines in T12), public so that the inputs can state it.</summary>
public static class NameLimit
{
    /// <summary>At most this many characters once trimmed: the length of the name columns in the database.</summary>
    public const int MaxLength = 100;
}
