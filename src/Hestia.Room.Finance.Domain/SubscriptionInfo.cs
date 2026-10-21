namespace Hestia.Room.Finance.Domain;

/// <summary>Extra information carried by a subscription (a recurring expense).</summary>
public sealed record SubscriptionInfo(DateOnly? CommitmentEndDate = null);
