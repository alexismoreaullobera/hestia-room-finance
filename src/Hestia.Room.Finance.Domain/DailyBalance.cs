namespace Hestia.Room.Finance.Domain;

/// <summary>Forecast balance of the account at the end of a day.</summary>
public sealed record DailyBalance(DateOnly Date, decimal Balance);
