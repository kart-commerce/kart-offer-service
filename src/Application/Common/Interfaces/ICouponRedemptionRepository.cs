using KartOfferService.Domain.Coupons;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>Write-side (PostgreSQL) access to the `coupon_redemptions` table - the real double-redemption guard (edge-cases.md #1) lives on its `(coupon_code, order_id)` unique constraint.</summary>
public interface ICouponRedemptionRepository
{
    Task AddAsync(CouponRedemption redemption, CancellationToken cancellationToken);

    Task<CouponRedemption?> GetAsync(string couponCode, string orderId, CancellationToken cancellationToken);

    /// <summary>Count of this user's active (non-voided) redemptions for this coupon - backs <see cref="RedemptionLimit.HasPerUserCapacity"/>.</summary>
    Task<int> CountActiveByUserAsync(string couponCode, string userId, CancellationToken cancellationToken);

    /// <summary>OFF-3: every active redemption tied to a cancelled order - a coupon code is not known ahead of time from `OrderCancelled`'s own payload (orderId, reason only).</summary>
    Task<IReadOnlyList<CouponRedemption>> GetActiveByOrderIdAsync(string orderId, CancellationToken cancellationToken);
}
