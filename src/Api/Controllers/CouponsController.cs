using KartOfferService.Api.Common;
using KartOfferService.Api.Security;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.DeactivateCoupon;
using KartOfferService.Application.Features.GetCouponAdminView;
using KartOfferService.Application.Features.IssueCoupon;
using KartOfferService.Application.Features.RedeemCoupon;
using KartOfferService.Application.Features.ValidateCoupon;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartOfferService.Api.Controllers;

[ApiController]
[Route("v1/coupons")]
public sealed class CouponsController : ControllerBase
{
    private readonly ISender _sender;

    public CouponsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>OFF-1: api-contract.yaml `POST /v1/coupons/validate` - checkout-path, read-only.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateCouponResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateCouponResponse>> Validate([FromBody] ValidateCouponRequest request, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new ValidateCouponQuery(request.CouponCode, request.UserId, request.CartTotal), cancellationToken);
        return Ok(response);
    }

    /// <summary>OFF-2: api-contract.yaml `POST /v1/coupons/redeem` - requires `Idempotency-Key`.</summary>
    [HttpPost("redeem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemCouponRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new RedeemCouponCommand(request.CouponCode, request.UserId, request.OrderId), cancellationToken);
        return result.IsSuccess ? Ok() : this.MapFailure(result.Error);
    }

    /// <summary>Not its own ticket - api-contract.yaml `GET /v1/coupons/{couponCode}` (admin-only), needed to obtain `version` for the deactivate `If-Match` precondition.</summary>
    [HttpGet("{couponCode}")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(CouponAdminViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CouponAdminViewDto>> GetAdminView([FromRoute] string couponCode, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetCouponAdminViewQuery(couponCode), cancellationToken);
        return this.ToActionResult<CouponAdminViewDto, CouponAdminViewDto>(result, dto => Ok(dto));
    }

    /// <summary>OFF-9: api-contract.yaml `POST /v1/coupons` (admin-only, ADR-0010). Requires `Idempotency-Key`.</summary>
    [HttpPost]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(CouponAdminViewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CouponAdminViewDto>> Issue(
        [FromBody] IssueCouponRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new IssueCouponCommand(request.CouponCode, request.PerUserCap, request.GlobalCap, request.ValidFrom, request.ValidUntil),
            cancellationToken);
        return this.ToActionResult<CouponAdminViewDto, CouponAdminViewDto>(
            result, dto => CreatedAtAction(nameof(GetAdminView), new { couponCode = dto.CouponCode }, dto));
    }

    /// <summary>OFF-10: api-contract.yaml `POST /v1/coupons/{couponCode}/deactivate` (admin-only). Requires `Idempotency-Key` + `If-Match`.</summary>
    [HttpPost("{couponCode}/deactivate")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(CouponAdminViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<CouponAdminViewDto>> Deactivate(
        [FromRoute] string couponCode,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromHeader(Name = "If-Match")] int ifMatch,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new DeactivateCouponCommand(couponCode, ifMatch), cancellationToken);
        return this.ToActionResult<CouponAdminViewDto, CouponAdminViewDto>(result, dto => Ok(dto));
    }
}

/// <summary>api-contract.yaml `validateCoupon` requestBody shape.</summary>
public sealed record ValidateCouponRequest(string CouponCode, string UserId, MoneyDto CartTotal);

/// <summary>api-contract.yaml `redeemCoupon` requestBody shape.</summary>
public sealed record RedeemCouponRequest(string CouponCode, string UserId, string OrderId);

/// <summary>api-contract.yaml `issueCoupon` requestBody shape.</summary>
public sealed record IssueCouponRequest(string CouponCode, int? PerUserCap, int? GlobalCap, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
