namespace Hestia.Room.Finance.Domain;

/// <summary>
/// The settings of the monthly plan: the starting balance of the month (solde de départ du mois),
/// 0 € by default. It can be negative: the account may already be overdrawn.
/// </summary>
public sealed record MonthPlan(YearMonth Month, decimal StartingBalance = 0m);
