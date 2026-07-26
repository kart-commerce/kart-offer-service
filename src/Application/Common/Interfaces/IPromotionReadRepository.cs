using KartOfferService.Application.Common.Models;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>Read-side (MongoDB, sharded) access to the denormalized active-PromotionCampaign projection. See <see cref="ActivePromotionReadModel"/>.</summary>
public interface IPromotionReadRepository
{
    Task<IReadOnlyList<ActivePromotionReadModel>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
