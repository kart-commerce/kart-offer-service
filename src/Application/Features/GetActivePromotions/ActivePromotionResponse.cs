namespace KartOfferService.Application.Features.GetActivePromotions;

public sealed record ActivePromotionResponse(Guid CampaignId, CampaignWindowResponse Window);

public sealed record CampaignWindowResponse(DateTimeOffset StartsAt, DateTimeOffset EndsAt);
