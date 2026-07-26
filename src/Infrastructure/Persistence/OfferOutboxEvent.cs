using System.Text.Json;
using Kart.Shared.Domain;
using KartOfferService.Domain.Coupons;
using KartOfferService.Domain.Pricing;
using KartOfferService.Domain.Promotions;

namespace KartOfferService.Infrastructure.Persistence;

/// <summary>
/// database-design.md's transactional outbox row - one per domain event raised across this
/// service's three aggregates, written in the same `SaveChangesAsync` call as the write it
/// describes (see <see cref="OfferDbContext.SaveChangesAsync"/>). `EventType` is looked up against
/// `contracts/message-bus-manifest.json`'s `publishedEvents` by <see cref="Messaging.OutboxRelayHostedService"/>
/// to resolve the exchange/routing key - nothing here is RabbitMQ-specific.
/// </summary>
public sealed class OfferOutboxEvent : OutboxEventBase
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new(JsonSerializerDefaults.Web);

    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = "system:offer-outbox-relay";

    private OfferOutboxEvent()
    {
    }

    private OfferOutboxEvent(Guid id, Guid aggregateId, string eventType, string payload, DateTimeOffset occurredAt)
        : base(id, aggregateId, eventType, payload, occurredAt)
    {
    }

    public static OfferOutboxEvent FromDomainEvent(IDomainEvent domainEvent, Guid aggregateId, string actingPrincipal)
    {
        var (eventType, payload) = domainEvent switch
        {
            CouponIssuedDomainEvent e => ("CouponIssued", (object)new
            {
                code = e.CouponCode,
                perUserCap = e.PerUserCap,
                globalCap = e.GlobalCap,
                validFrom = e.ValidFrom,
                validUntil = e.ValidUntil,
            }),
            CouponRedeemedDomainEvent e => ("CouponRedeemed", new { code = e.CouponCode, userId = e.UserId, orderId = e.OrderId }),
            CouponRedemptionVoidedDomainEvent e => ("CouponRedemptionVoided", new { code = e.CouponCode, orderId = e.OrderId, voidedAt = e.VoidedAt }),
            CouponDeactivatedDomainEvent e => ("CouponDeactivated", new { code = e.CouponCode, deactivatedAt = e.DeactivatedAt }),
            PriceQuoteIssuedDomainEvent e => ("PriceQuoteIssued", new
            {
                quoteId = e.QuoteId,
                total = new { amount = e.Total.Amount, currency = e.Total.Currency },
                expiresAt = e.ExpiresAt,
            }),
            PromotionActivatedDomainEvent e => ("PromotionActivated", new
            {
                campaignId = e.CampaignId,
                window = new { startsAt = e.StartsAt, endsAt = e.EndsAt },
            }),
            PromotionDeactivatedDomainEvent e => ("PromotionDeactivated", new { campaignId = e.CampaignId, deactivatedAt = e.DeactivatedAt }),
            _ => throw new InvalidOperationException($"No outbox payload mapping for domain event '{domainEvent.GetType().Name}'."),
        };

        return new OfferOutboxEvent(Guid.NewGuid(), aggregateId, eventType, JsonSerializer.Serialize(payload, PayloadSerializerOptions), domainEvent.OccurredAt)
        {
            CreatedBy = actingPrincipal,
        };
    }
}
