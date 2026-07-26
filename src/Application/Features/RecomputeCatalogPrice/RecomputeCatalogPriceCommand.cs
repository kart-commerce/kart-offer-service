using Kart.Shared.Domain;
using MediatR;

namespace KartOfferService.Application.Features.RecomputeCatalogPrice;

/// <summary>OFF-4: consumes `ProductPriceChanged` (sku, oldPrice, newPrice) - materializes the new price locally so `/pricing/quote` (OFF-8) never needs a synchronous fan-out to Product (architecture.md).</summary>
public sealed record RecomputeCatalogPriceCommand(string Sku, decimal NewPrice) : IRequest<Result>;
