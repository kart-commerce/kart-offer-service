using Kart.Shared.Domain;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.GetCouponAdminView;

/// <summary>
/// api-contract.yaml `GET /v1/coupons/{couponCode}` - not its own ticket in tickets.md, but needed
/// so a caller can obtain the current `version` before `POST /v1/coupons/{couponCode}/deactivate`'s
/// `If-Match` precondition. Reads PostgreSQL directly (never the Mongo projection) since a stale
/// version handed back here would make the very next deactivate call 412 immediately.
/// </summary>
public sealed record GetCouponAdminViewQuery(string CouponCode) : IRequest<Result<CouponAdminViewDto>>;
