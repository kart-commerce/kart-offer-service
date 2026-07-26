using Kart.Shared.Domain;

namespace KartOfferService.Domain.Promotions;

/// <summary>event-contract.md `PromotionActivated` - key fields `campaignId`, `window`.</summary>
public sealed record PromotionActivatedDomainEvent(
    Guid CampaignId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>event-contract.md `PromotionDeactivated` - symmetric counterpart to `PromotionActivated`, fired only on explicit deactivation.</summary>
public sealed record PromotionDeactivatedDomainEvent(
    Guid CampaignId,
    DateTimeOffset DeactivatedAt,
    DateTimeOffset OccurredAt) : IDomainEvent;
