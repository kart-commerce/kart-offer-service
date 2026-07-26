using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.ValidateCoupon;

/// <summary>OFF-1: api-contract.yaml `POST /v1/coupons/validate` - read-only, never redeems.</summary>
public sealed record ValidateCouponQuery(string CouponCode, string UserId, MoneyDto CartTotal) : IRequest<ValidateCouponResponse>;
