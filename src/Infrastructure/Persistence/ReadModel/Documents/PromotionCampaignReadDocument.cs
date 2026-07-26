using MongoDB.Bson.Serialization.Attributes;

namespace KartOfferService.Infrastructure.Persistence.ReadModel.Documents;

/// <summary>CQRS read-side, denormalized copy of an active/recently-changed `promotion_campaigns` row - `_id = campaignId`.</summary>
public sealed class PromotionCampaignReadDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("startsAt")]
    public DateTime StartsAt { get; set; }

    [BsonElement("endsAt")]
    public DateTime EndsAt { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; }
}
