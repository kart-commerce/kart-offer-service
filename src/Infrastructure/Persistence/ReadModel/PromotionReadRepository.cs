using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MongoDB.Driver;

namespace KartOfferService.Infrastructure.Persistence.ReadModel;

public sealed class PromotionReadRepository : IPromotionReadRepository
{
    private readonly OfferReadDbContext _context;

    public PromotionReadRepository(OfferReadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ActivePromotionReadModel>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var nowUtc = now.UtcDateTime;
        var filter = Builders<Documents.PromotionCampaignReadDocument>.Filter.And(
            Builders<Documents.PromotionCampaignReadDocument>.Filter.Lte(d => d.StartsAt, nowUtc),
            Builders<Documents.PromotionCampaignReadDocument>.Filter.Gt(d => d.EndsAt, nowUtc));

        var documents = await _context.PromotionCampaigns.Find(filter).ToListAsync(cancellationToken);
        return documents
            .Select(d => new ActivePromotionReadModel(Guid.Parse(d.Id), new DateTimeOffset(d.StartsAt, TimeSpan.Zero), new DateTimeOffset(d.EndsAt, TimeSpan.Zero)))
            .ToList();
    }
}
