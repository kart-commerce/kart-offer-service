using Kart.Shared.Domain;
using MediatR;

namespace KartOfferService.Application.Features.GetPricingQuote;

/// <summary>OFF-8: api-contract.yaml `POST /v1/pricing/quote`. A command, not a query - it writes a new, immutable PricingQuote row (ddd-model.md) and publishes `PriceQuoteIssued`.</summary>
public sealed record GetPricingQuoteCommand(IReadOnlyList<PricingLineItem> Items, string Currency) : IRequest<Result<PricingQuoteResponse>>;

public sealed record PricingLineItem(string Sku, int Quantity);
