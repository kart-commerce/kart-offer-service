using KartOfferService.Infrastructure.Persistence.ReadModel.Documents;
using MongoDB.Driver;

namespace KartOfferService.Infrastructure.Persistence.ReadModel;

/// <summary>
/// The write path for this service's CQRS read side - called exclusively by
/// <see cref="Messaging.ReadModelProjectionConsumerHostedService"/>, never by a request handler.
/// Every method is an idempotent upsert/set so at-least-once delivery of the same event never
/// corrupts the projection.
/// </summary>
public sealed class ReadModelProjectionWriter
{
    private readonly OfferReadDbContext _context;

    public ReadModelProjectionWriter(OfferReadDbContext context)
    {
        _context = context;
    }

    /// <summary>`CouponIssued` - the initial snapshot. `totalRedemptions` is only ever set on insert; later events increment it in place.</summary>
    public Task UpsertCouponAsync(string couponCode, int? perUserCap, int? globalCap, DateTime validFrom, DateTime validUntil, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<CouponReadDocument>.Update
            .Set(d => d.PerUserCap, perUserCap)
            .Set(d => d.GlobalCap, globalCap)
            .Set(d => d.ValidFrom, validFrom)
            .Set(d => d.ValidUntil, validUntil)
            .SetOnInsert(d => d.TotalRedemptions, 0)
            .Set(d => d.UpdatedAt, updatedAt);

        return _context.Coupons.UpdateOneAsync(d => d.Id == couponCode, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    /// <summary>`CouponRedeemed` (+1) / `CouponRedemptionVoided` (-1).</summary>
    public Task IncrementCouponRedemptionsAsync(string couponCode, int delta, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<CouponReadDocument>.Update.Inc(d => d.TotalRedemptions, delta).Set(d => d.UpdatedAt, updatedAt);
        return _context.Coupons.UpdateOneAsync(d => d.Id == couponCode, update, cancellationToken: cancellationToken);
    }

    /// <summary>`CouponDeactivated` - the early-truncated `validUntil`.</summary>
    public Task UpdateCouponValidUntilAsync(string couponCode, DateTime validUntil, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<CouponReadDocument>.Update.Set(d => d.ValidUntil, validUntil).Set(d => d.UpdatedAt, updatedAt);
        return _context.Coupons.UpdateOneAsync(d => d.Id == couponCode, update, cancellationToken: cancellationToken);
    }

    /// <summary>`PromotionActivated` - the initial snapshot.</summary>
    public Task UpsertPromotionAsync(string campaignId, DateTime startsAt, DateTime endsAt, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<PromotionCampaignReadDocument>.Update
            .Set(d => d.StartsAt, startsAt)
            .Set(d => d.EndsAt, endsAt)
            .Set(d => d.UpdatedAt, updatedAt);

        return _context.PromotionCampaigns.UpdateOneAsync(d => d.Id == campaignId, update, new UpdateOptions { IsUpsert = true }, cancellationToken);
    }

    /// <summary>`PromotionDeactivated` - the early-truncated `endsAt`.</summary>
    public Task UpdatePromotionEndsAtAsync(string campaignId, DateTime endsAt, DateTime updatedAt, CancellationToken cancellationToken)
    {
        var update = Builders<PromotionCampaignReadDocument>.Update.Set(d => d.EndsAt, endsAt).Set(d => d.UpdatedAt, updatedAt);
        return _context.PromotionCampaigns.UpdateOneAsync(d => d.Id == campaignId, update, cancellationToken: cancellationToken);
    }
}
