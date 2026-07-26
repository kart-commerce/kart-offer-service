namespace KartOfferService.Infrastructure.Messaging;

/// <summary>event-contract.md `ProductPriceChanged` (consumed from Product) - key fields `sku`, `oldPrice`, `newPrice`.</summary>
public sealed record ProductPriceChangedEventPayload(string Sku, decimal OldPrice, decimal NewPrice);

/// <summary>event-contract.md `OrderCancelled` (consumed from Order) - key fields `orderId`, `reason`.</summary>
public sealed record OrderCancelledEventPayload(string OrderId, string Reason);
