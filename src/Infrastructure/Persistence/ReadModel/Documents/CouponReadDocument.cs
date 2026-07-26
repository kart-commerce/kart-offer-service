using MongoDB.Bson.Serialization.Attributes;

namespace KartOfferService.Infrastructure.Persistence.ReadModel.Documents;

/// <summary>
/// CQRS read-side, denormalized copy of the write-side `coupons` row - `_id = couponCode`
/// directly. Kept in sync from PostgreSQL via the outbox -> RabbitMQ -> read-model-projection
/// pipeline (Infrastructure/Messaging/ReadModelProjectionConsumerHostedService), never written by
/// any request handler directly.
/// </summary>
public sealed class CouponReadDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("perUserCap")]
    public int? PerUserCap { get; set; }

    [BsonElement("globalCap")]
    public int? GlobalCap { get; set; }

    [BsonElement("validFrom")]
    public DateTime ValidFrom { get; set; }

    [BsonElement("validUntil")]
    public DateTime ValidUntil { get; set; }

    [BsonElement("totalRedemptions")]
    public int TotalRedemptions { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
