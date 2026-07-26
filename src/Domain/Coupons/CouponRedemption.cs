namespace KartOfferService.Domain.Coupons;

/// <summary>
/// database-design.md `coupon_redemptions` - one row per successful redemption, uniquely keyed on
/// `(coupon_code, order_id)` (edge-cases.md #1, the real double-redemption guard; <see cref="Coupon.Redeem"/>'s
/// own window/cap check is the concurrency-lock-protected complement, not a substitute). A child
/// entity of the <see cref="Coupon"/> aggregate, persisted in its own table/repository so
/// per-user redemption counts and the unique constraint can be queried/enforced independently.
/// </summary>
public sealed class CouponRedemption
{
    public Guid Id { get; private set; }
    public string CouponCode { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string OrderId { get; private set; } = string.Empty;
    public DateTimeOffset RedeemedAt { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private CouponRedemption()
    {
    }

    internal static CouponRedemption Create(string couponCode, string userId, string orderId, DateTimeOffset now, string actingPrincipal) => new()
    {
        Id = Guid.NewGuid(),
        CouponCode = couponCode,
        UserId = userId,
        OrderId = orderId,
        RedeemedAt = now,
        UpdatedAt = now,
        CreatedBy = actingPrincipal,
        UpdatedBy = actingPrincipal,
    };

    /// <summary>OFF-3: marks this redemption voided. Idempotent - a no-op if already voided.</summary>
    public void Void(DateTimeOffset now, string actingPrincipal)
    {
        if (VoidedAt is not null)
        {
            return;
        }

        VoidedAt = now;
        UpdatedAt = now;
        UpdatedBy = actingPrincipal;
    }
}
