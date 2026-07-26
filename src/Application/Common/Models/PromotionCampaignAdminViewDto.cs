using KartOfferService.Domain.Promotions;

namespace KartOfferService.Application.Common.Models;

/// <summary>api-contract.yaml's `PromotionCampaignAdminView` schema.</summary>
public sealed record PromotionCampaignAdminViewDto(
    Guid CampaignId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DiscountRuleDto DiscountRule,
    int Version)
{
    public static PromotionCampaignAdminViewDto FromDomain(PromotionCampaign campaign) => new(
        campaign.Id,
        campaign.Window.StartsAt,
        campaign.Window.EndsAt,
        DiscountRuleDto.FromDomain(campaign.DiscountRule),
        campaign.Version);
}
