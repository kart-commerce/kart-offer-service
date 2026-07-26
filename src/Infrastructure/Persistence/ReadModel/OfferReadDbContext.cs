using KartOfferService.Infrastructure.Persistence.ReadModel.Documents;
using MongoDB.Driver;

namespace KartOfferService.Infrastructure.Persistence.ReadModel;

/// <summary>
/// Typed accessor for this service's two denormalized MongoDB read collections - the CQRS query
/// side. Deployed against a sharded MongoDB cluster in production (the user's explicit
/// requirement); nothing in this class assumes a single node.
/// </summary>
public sealed class OfferReadDbContext
{
    public const string CouponsCollectionName = "coupons_read";
    public const string PromotionCampaignsCollectionName = "promotion_campaigns_read";

    public IMongoDatabase Database { get; }

    public OfferReadDbContext(IMongoDatabase database)
    {
        Database = database;
    }

    public IMongoCollection<CouponReadDocument> Coupons => Database.GetCollection<CouponReadDocument>(CouponsCollectionName);

    public IMongoCollection<PromotionCampaignReadDocument> PromotionCampaigns => Database.GetCollection<PromotionCampaignReadDocument>(PromotionCampaignsCollectionName);
}
