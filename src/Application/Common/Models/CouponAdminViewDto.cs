namespace KartOfferService.Application.Common.Models;

/// <summary>api-contract.yaml's `CouponAdminView` schema - `version` is the optimistic-concurrency token passed back as `If-Match` on deactivate.</summary>
public sealed record CouponAdminViewDto(
    string CouponCode,
    int? PerUserCap,
    int? GlobalCap,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    int TotalRedemptions,
    int Version)
{
    public static CouponAdminViewDto FromDomain(Domain.Coupons.Coupon coupon) => new(
        coupon.CouponCode,
        coupon.PerUserCap,
        coupon.GlobalCap,
        coupon.ValidFrom,
        coupon.ValidUntil,
        coupon.TotalRedemptions,
        coupon.Version);
}
