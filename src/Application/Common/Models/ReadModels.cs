namespace KartOfferService.Application.Common.Models;

/// <summary>
/// CQRS read-side projection of a Coupon - denormalized into MongoDB, kept in sync from the
/// PostgreSQL write side via the outbox -> RabbitMQ -> read-model-projection pipeline
/// (Infrastructure/Messaging/ReadModelProjectionConsumerHostedService). Powers the high-throughput
/// checkout-path `POST /v1/coupons/validate` read (OFF-1) - eventual consistency is acceptable
/// there because the authoritative, strongly-consistent redeem path (OFF-2) always re-validates
/// against PostgreSQL under a row lock regardless of what this read returned.
/// </summary>
public sealed record CouponReadModel(
    string CouponCode,
    int? PerUserCap,
    int? GlobalCap,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int TotalRedemptions);

/// <summary>CQRS read-side projection of an active PromotionCampaign window - denormalized into MongoDB for OFF-7's `GET /v1/promotions/active`.</summary>
public sealed record ActivePromotionReadModel(Guid CampaignId, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
