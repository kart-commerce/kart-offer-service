namespace KartOfferService.Application.Common;

/// <summary>Well-known non-HTTP-triggered actors stamped as `createdBy`/`updatedBy` when a write is driven by a consumed event rather than an authenticated caller.</summary>
public static class SystemPrincipals
{
    public const string ProductEventsConsumer = "system:product-events-consumer";
    public const string OrderEventsConsumer = "system:order-events-consumer";
    public const string Unknown = "system:unknown";
}
