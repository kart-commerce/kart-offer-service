using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Promotions;
using Microsoft.EntityFrameworkCore;

namespace KartOfferService.Infrastructure.Persistence;

public sealed class PromotionCampaignRepository : IPromotionCampaignRepository
{
    private readonly OfferDbContext _dbContext;

    public PromotionCampaignRepository(OfferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<PromotionCampaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken) =>
        _dbContext.PromotionCampaigns.FirstOrDefaultAsync(c => c.Id == campaignId, cancellationToken);

    public Task<PromotionCampaign?> GetForUpdateAsync(Guid campaignId, CancellationToken cancellationToken) =>
        _dbContext.PromotionCampaigns
            .FromSqlInterpolated($"SELECT * FROM promotion_campaigns WHERE campaign_id = {campaignId} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PromotionCampaign>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await _dbContext.PromotionCampaigns
            .Where(c => now >= c.Window.StartsAt && now < c.Window.EndsAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(PromotionCampaign campaign, CancellationToken cancellationToken) =>
        await _dbContext.PromotionCampaigns.AddAsync(campaign, cancellationToken);
}
