using System.Security.Cryptography;
using System.Text;

namespace KartOfferService.Infrastructure.Persistence;

/// <summary>
/// `offer_outbox_events.aggregate_id` is a `Guid` (Kart.Shared.Domain.OutboxEventBase), but
/// <see cref="Domain.Coupons.Coupon"/>'s real identity is its natural string `CouponCode`
/// (see Coupon's own remarks on why it doesn't inherit <c>AggregateRoot</c>). This derives a
/// stable, deterministic Guid from the code purely for outbox-row correlation/traceability -
/// event dispatch itself is keyed by `EventType` via the message-bus manifest, never by this value.
/// </summary>
public static class AggregateIdHasher
{
    public static Guid ForCouponCode(string couponCode)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(couponCode));
        return new Guid(hash.AsSpan(0, 16));
    }
}
