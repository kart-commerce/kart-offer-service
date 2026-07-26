using MediatR;

namespace KartOfferService.Application.Features.GetActivePromotions;

/// <summary>
/// OFF-7: api-contract.yaml `GET /v1/promotions/active`. `Sku` is accepted per the contract but
/// currently unused - database-design.md's `promotion_campaigns` table has no SKU association
/// (campaigns are platform-wide, not per-product), so there is nothing to filter by yet. A
/// documented scope simplification, not a defect.
/// </summary>
public sealed record GetActivePromotionsQuery(string? Sku) : IRequest<IReadOnlyList<ActivePromotionResponse>>;
