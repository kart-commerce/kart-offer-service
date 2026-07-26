using KartOfferService.Application.Common.Models;

namespace KartOfferService.Application.Features.GetPricingQuote;

public sealed record PricingQuoteResponse(Guid QuoteId, MoneyDto Total, DateTimeOffset ExpiresAt);
