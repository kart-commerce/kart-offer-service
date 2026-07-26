using KartOfferService.Application.Common.Models;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>Read-side (MongoDB, sharded) access to the denormalized Coupon projection. See <see cref="CouponReadModel"/>.</summary>
public interface ICouponReadRepository
{
    Task<CouponReadModel?> GetAsync(string couponCode, CancellationToken cancellationToken);
}
