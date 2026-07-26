using Kart.Shared.Domain;
using MediatR;

namespace KartOfferService.Application.Features.RedeemCoupon;

/// <summary>OFF-2: api-contract.yaml `POST /v1/coupons/redeem` - requires `Idempotency-Key` (dedup served by `coupon_redemptions`'s own `(coupon_code, order_id)` unique key, see the Handler).</summary>
public sealed record RedeemCouponCommand(string CouponCode, string UserId, string OrderId) : IRequest<Result>;
