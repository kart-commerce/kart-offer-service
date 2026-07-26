namespace KartOfferService.Application.Common.Exceptions;

/// <summary>
/// Thrown by Infrastructure when a write violates a natural-key uniqueness constraint
/// (`coupons.coupon_code` PRIMARY KEY) that a handler's own pre-check raced against - the
/// TOCTOU-safe backstop behind <see cref="Features.IssueCoupon.IssueCouponCommandHandler"/>'s
/// existence check. Mapped to 409 via `Kart.Shared.ErrorHandling`'s exception-mapping registry
/// (Api layer), never caught/translated locally per kart-conventions.md.
/// </summary>
public sealed class DuplicateKeyException : Exception
{
    public DuplicateKeyException(string message) : base(message)
    {
    }
}
