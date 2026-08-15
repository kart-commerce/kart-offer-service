using KartOfferService.Api.Common;
using KartOfferService.Api.Security;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.DeactivateCoupon;
using KartOfferService.Application.Features.GetCouponAdminView;
using KartOfferService.Application.Features.IssueCoupon;
using KartOfferService.Application.Features.RedeemCoupon;
using KartOfferService.Application.Features.ValidateCoupon;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartOfferService.Api.Controllers;

[ApiController]
[Route("v1/coupons")]
public sealed class CouponsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<CouponsController> _logger;

    public CouponsController(ISender sender, ILogger<CouponsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>OFF-1: api-contract.yaml `POST /v1/coupons/validate` - checkout-path, read-only. business-flows.md flow #1's "Coupon" step.</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(ValidateCouponResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ValidateCouponResponse>> Validate([FromBody] ValidateCouponRequest request, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.NormalShoppingPurchaseJourney);
        _logger.LogInformation("Stage {Stage}: validate-coupon request received for {CouponCode}", "ValidateCouponRequestReceived", request.CouponCode);

        var query = new ValidateCouponQuery(request.CouponCode, request.UserId, request.CartTotal);
        _logger.LogInformation("Stage {Stage}: dispatching ValidateCouponQuery for {CouponCode}", "ValidateCouponQueryDispatched", request.CouponCode);
        var response = await _sender.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>OFF-2: api-contract.yaml `POST /v1/coupons/redeem` - requires `Idempotency-Key`. business-flows.md flow #1's "Coupon" step.</summary>
    [HttpPost("redeem")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemCouponRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.NormalShoppingPurchaseJourney);
        _logger.LogInformation("Stage {Stage}: redeem-coupon request received for {CouponCode}, order {OrderId}", "RedeemCouponRequestReceived", request.CouponCode, request.OrderId);

        var command = new RedeemCouponCommand(request.CouponCode, request.UserId, request.OrderId);
        _logger.LogInformation("Stage {Stage}: dispatching RedeemCouponCommand for {CouponCode}, order {OrderId}", "RedeemCouponCommandDispatched", request.CouponCode, request.OrderId);
        var result = await _sender.Send(command, cancellationToken);
        return result.IsSuccess ? Ok() : this.MapFailure(result.Error);
    }

    /// <summary>Not its own ticket - api-contract.yaml `GET /v1/coupons/{couponCode}` (admin-only), needed to obtain `version` for the deactivate `If-Match` precondition.</summary>
    [HttpGet("{couponCode}")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(CouponAdminViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CouponAdminViewDto>> GetAdminView([FromRoute] string couponCode, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: get-coupon-admin-view request received for {CouponCode}", "GetCouponAdminViewRequestReceived", couponCode);

        var query = new GetCouponAdminViewQuery(couponCode);
        _logger.LogInformation("Stage {Stage}: dispatching GetCouponAdminViewQuery for {CouponCode}", "GetCouponAdminViewQueryDispatched", couponCode);
        var result = await _sender.Send(query, cancellationToken);
        return this.ToActionResult<CouponAdminViewDto, CouponAdminViewDto>(result, dto => Ok(dto));
    }

    /// <summary>OFF-9: api-contract.yaml `POST /v1/coupons` (admin-only, ADR-0010). Requires `Idempotency-Key`. business-flows.md flow #12's "Create Coupon" step.</summary>
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
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: issue-coupon request received for {CouponCode}", "IssueCouponRequestReceived", request.CouponCode);

        var command = new IssueCouponCommand(request.CouponCode, request.PerUserCap, request.GlobalCap, request.ValidFrom, request.ValidUntil);
        _logger.LogInformation("Stage {Stage}: dispatching IssueCouponCommand for {CouponCode}", "IssueCouponCommandDispatched", request.CouponCode);
        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult<CouponAdminViewDto, CouponAdminViewDto>(
            result, dto => CreatedAtAction(nameof(GetAdminView), new { couponCode = dto.CouponCode }, dto));
    }

    /// <summary>OFF-10: api-contract.yaml `POST /v1/coupons/{couponCode}/deactivate` (admin-only). Requires `Idempotency-Key` + `If-Match`. business-flows.md flow #12's "Expire/Deactivate" step.</summary>
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
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: deactivate-coupon request received for {CouponCode}", "DeactivateCouponRequestReceived", couponCode);

        var command = new DeactivateCouponCommand(couponCode, ifMatch);
        _logger.LogInformation("Stage {Stage}: dispatching DeactivateCouponCommand for {CouponCode}", "DeactivateCouponCommandDispatched", couponCode);
        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult<CouponAdminViewDto, CouponAdminViewDto>(result, dto => Ok(dto));
    }
}

/// <summary>api-contract.yaml `validateCoupon` requestBody shape.</summary>
public sealed record ValidateCouponRequest(string CouponCode, string UserId, MoneyDto CartTotal);

/// <summary>api-contract.yaml `redeemCoupon` requestBody shape.</summary>
public sealed record RedeemCouponRequest(string CouponCode, string UserId, string OrderId);

/// <summary>api-contract.yaml `issueCoupon` requestBody shape.</summary>
public sealed record IssueCouponRequest(string CouponCode, int? PerUserCap, int? GlobalCap, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil);
