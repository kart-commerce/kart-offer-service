using Kart.Shared.Domain;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.DeactivateCoupon;

/// <summary>OFF-10: api-contract.yaml `POST /v1/coupons/{couponCode}/deactivate` - `ExpectedVersion` is the caller's `If-Match` header.</summary>
public sealed record DeactivateCouponCommand(string CouponCode, int ExpectedVersion) : IRequest<Result<CouponAdminViewDto>>;
