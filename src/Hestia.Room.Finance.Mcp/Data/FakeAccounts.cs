namespace Hestia.Room.Finance.Mcp.Data;

public sealed record Account(string Name, decimal Balance);

/// <summary>
/// Hard-coded fake data for the T1 spike. Replaced by a real data store in Finances v1.
/// </summary>
public static class FakeAccounts
{
    public static IReadOnlyList<Account> All { get; } =
    [
        new("Compte courant", 1_250.00m),
        new("Livret A", 2_000.00m),
    ];
}
