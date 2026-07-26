using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Coupons;
using MediatR;

namespace KartOfferService.Application.Features.ValidateCoupon;

/// <summary>
/// Reads the MongoDB-denormalized <see cref="Common.Models.CouponReadModel"/> (CQRS query side) -
/// this is the high-throughput checkout-path read, so it deliberately trades per-user-cap
/// precision for speed: it checks window and global cap only. Per-user cap is enforced
/// authoritatively at redeem time (OFF-2, against PostgreSQL under a row lock) regardless of what
/// this advisory check returns - api-contract.yaml is explicit that this endpoint "does not redeem".
/// </summary>
public sealed class ValidateCouponQueryHandler : IRequestHandler<ValidateCouponQuery, ValidateCouponResponse>
{
    private readonly ICouponReadRepository _couponReads;
    private readonly TimeProvider _timeProvider;

    public ValidateCouponQueryHandler(ICouponReadRepository couponReads, TimeProvider timeProvider)
    {
        _couponReads = couponReads;
        _timeProvider = timeProvider;
    }

    public async Task<ValidateCouponResponse> Handle(ValidateCouponQuery request, CancellationToken cancellationToken)
    {
        var coupon = await _couponReads.GetAsync(request.CouponCode, cancellationToken);
        if (coupon is null)
        {
            return new ValidateCouponResponse(false, "Coupon not found.");
        }

        var now = _timeProvider.GetUtcNow();
        var limit = new RedemptionLimit(coupon.PerUserCap, coupon.GlobalCap, coupon.ValidFrom, coupon.ValidUntil);

        if (!limit.IsWithinWindow(now))
        {
            return new ValidateCouponResponse(false, "Coupon is not within its valid window.");
        }

        if (!limit.HasGlobalCapacity(coupon.TotalRedemptions))
        {
            return new ValidateCouponResponse(false, "Coupon has reached its global redemption cap.");
        }

        return new ValidateCouponResponse(true, null);
    }
}
