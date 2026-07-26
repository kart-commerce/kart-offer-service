using Kart.Shared.Domain;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.IssueCoupon;

/// <summary>OFF-9: api-contract.yaml `POST /v1/coupons` (admin-only, ADR-0010). Publishes `CouponIssued`.</summary>
public sealed record IssueCouponCommand(
    string CouponCode,
    int? PerUserCap,
    int? GlobalCap,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil) : IRequest<Result<CouponAdminViewDto>>;
