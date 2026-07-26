namespace KartOfferService.Domain.Promotions;

/// <summary>ddd-model.md's PromotionCampaign value object - the window a campaign is active within.</summary>
public sealed record CampaignWindow(DateTimeOffset StartsAt, DateTimeOffset EndsAt)
{
    public bool IsActive(DateTimeOffset now) => now >= StartsAt && now < EndsAt;
}
