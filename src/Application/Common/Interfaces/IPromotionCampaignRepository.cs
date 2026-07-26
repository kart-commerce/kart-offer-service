using KartOfferService.Domain.Promotions;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>
/// Write-side (PostgreSQL) access to the PromotionCampaign aggregate - also this service's
/// in-process source for pricing computation (architecture.md: Pricing reads Promotion
/// synchronously, same bounded context, never via the eventually-consistent Mongo read model -
/// money computation needs the freshest state, not read-optimized staleness).
/// </summary>
public interface IPromotionCampaignRepository
{
    Task<PromotionCampaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken);

    Task<PromotionCampaign?> GetForUpdateAsync(Guid campaignId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PromotionCampaign>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken);

    Task AddAsync(PromotionCampaign campaign, CancellationToken cancellationToken);
}
