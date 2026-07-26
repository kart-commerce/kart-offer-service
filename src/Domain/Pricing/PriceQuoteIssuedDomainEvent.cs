using Kart.Shared.Domain;

namespace KartOfferService.Domain.Pricing;

/// <summary>event-contract.md `PriceQuoteIssued` - key fields `quoteId`, `total`, `expiresAt`. Analytics-only consumer.</summary>
public sealed record PriceQuoteIssuedDomainEvent(
    Guid QuoteId,
    Money Total,
    DateTimeOffset ExpiresAt,
    DateTimeOffset OccurredAt) : IDomainEvent;
