using KartOfferService.Application.Common.Interfaces;
using MediatR;

namespace KartOfferService.Application.Features.GetActivePromotions;

/// <summary>Reads the MongoDB-denormalized, sharded active-campaign projection (CQRS query side) - this is the highest-QPS endpoint this service exposes.</summary>
public sealed class GetActivePromotionsQueryHandler : IRequestHandler<GetActivePromotionsQuery, IReadOnlyList<ActivePromotionResponse>>
{
    private readonly IPromotionReadRepository _promotionReads;
    private readonly TimeProvider _timeProvider;

    public GetActivePromotionsQueryHandler(IPromotionReadRepository promotionReads, TimeProvider timeProvider)
    {
        _promotionReads = promotionReads;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<ActivePromotionResponse>> Handle(GetActivePromotionsQuery request, CancellationToken cancellationToken)
    {
        var active = await _promotionReads.GetActiveAsync(_timeProvider.GetUtcNow(), cancellationToken);
        return active
            .Select(p => new ActivePromotionResponse(p.CampaignId, new CampaignWindowResponse(p.StartsAt, p.EndsAt)))
            .ToList();
    }
}
