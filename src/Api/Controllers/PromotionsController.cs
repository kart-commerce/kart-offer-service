using KartOfferService.Api.Common;
using KartOfferService.Api.Security;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.CreatePromotionCampaign;
using KartOfferService.Application.Features.DeactivatePromotionCampaign;
using KartOfferService.Application.Features.GetActivePromotions;
using KartOfferService.Application.Features.GetPromotionCampaignAdminView;
using Kart.Shared.Observability;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartOfferService.Api.Controllers;

/// <summary>Every action here belongs to business-flows.md flow #12 ("Offers, Coupons & Promotions (Admin)") - KartFlowContext.Push wraps each one so every downstream log line (handler, persistence, outbox) inherits the Flow tag.</summary>
[ApiController]
[Route("v1/promotions")]
public sealed class PromotionsController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ILogger<PromotionsController> _logger;

    public PromotionsController(ISender sender, ILogger<PromotionsController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    /// <summary>OFF-7: api-contract.yaml `GET /v1/promotions/active` - the highest-QPS endpoint this service exposes; reads the MongoDB read model. business-flows.md flow #12's "Publish"/"Applied at Checkout" outcome - this is how a published campaign becomes externally visible.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivePromotionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivePromotionResponse>>> GetActive([FromQuery] string? sku, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: get-active-promotions request received (sku {Sku})", "GetActivePromotionsRequestReceived", sku);

        var query = new GetActivePromotionsQuery(sku);
        _logger.LogInformation("Stage {Stage}: dispatching GetActivePromotionsQuery (sku {Sku})", "GetActivePromotionsQueryDispatched", sku);
        var response = await _sender.Send(query, cancellationToken);
        return Ok(response);
    }

    /// <summary>Not its own ticket - api-contract.yaml `GET /v1/promotions/{campaignId}` (admin-only), needed to obtain `version` for the deactivate `If-Match` precondition.</summary>
    [HttpGet("{campaignId:guid}")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(PromotionCampaignAdminViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionCampaignAdminViewDto>> GetAdminView([FromRoute] Guid campaignId, CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: get-promotion-campaign-admin-view request received for {CampaignId}", "GetPromotionCampaignAdminViewRequestReceived", campaignId);

        var query = new GetPromotionCampaignAdminViewQuery(campaignId);
        _logger.LogInformation("Stage {Stage}: dispatching GetPromotionCampaignAdminViewQuery for {CampaignId}", "GetPromotionCampaignAdminViewQueryDispatched", campaignId);
        var result = await _sender.Send(query, cancellationToken);
        return this.ToActionResult<PromotionCampaignAdminViewDto, PromotionCampaignAdminViewDto>(result, dto => Ok(dto));
    }

    /// <summary>OFF-5: api-contract.yaml `POST /v1/promotions` (admin-only, ADR-0019). Requires `Idempotency-Key`. Publishes `PromotionActivated`. business-flows.md flow #12's "Create Coupon/Offer" -> "Discount Type" -> "Publish" steps.</summary>
    [HttpPost]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(PromotionCampaignAdminViewDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PromotionCampaignAdminViewDto>> Create(
        [FromBody] CreatePromotionCampaignRequest request,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: create-promotion-campaign request received (starts {StartsAt}, ends {EndsAt})", "CreatePromotionCampaignRequestReceived", request.StartsAt, request.EndsAt);

        var command = new CreatePromotionCampaignCommand(request.StartsAt, request.EndsAt, request.DiscountRule);
        _logger.LogInformation("Stage {Stage}: dispatching CreatePromotionCampaignCommand", "CreatePromotionCampaignCommandDispatched");
        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult<PromotionCampaignAdminViewDto, PromotionCampaignAdminViewDto>(
            result, dto => CreatedAtAction(nameof(GetAdminView), new { campaignId = dto.CampaignId }, dto));
    }

    /// <summary>OFF-6: api-contract.yaml `POST /v1/promotions/{campaignId}/deactivate` (admin-only). Requires `Idempotency-Key` + `If-Match`. business-flows.md flow #12's "Expire/Deactivate" step.</summary>
    [HttpPost("{campaignId:guid}/deactivate")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(PromotionCampaignAdminViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<PromotionCampaignAdminViewDto>> Deactivate(
        [FromRoute] Guid campaignId,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromHeader(Name = "If-Match")] int ifMatch,
        CancellationToken cancellationToken)
    {
        using var _ = KartFlowContext.Push(FlowNames.OffersCouponsPromotionsManagementAdmin);
        _logger.LogInformation("Stage {Stage}: deactivate-promotion-campaign request received for {CampaignId}", "DeactivatePromotionCampaignRequestReceived", campaignId);

        var command = new DeactivatePromotionCampaignCommand(campaignId, ifMatch);
        _logger.LogInformation("Stage {Stage}: dispatching DeactivatePromotionCampaignCommand for {CampaignId}", "DeactivatePromotionCampaignCommandDispatched", campaignId);
        var result = await _sender.Send(command, cancellationToken);
        return this.ToActionResult<PromotionCampaignAdminViewDto, PromotionCampaignAdminViewDto>(result, dto => Ok(dto));
    }
}

/// <summary>api-contract.yaml `createPromotionCampaign` requestBody shape.</summary>
public sealed record CreatePromotionCampaignRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, DiscountRuleDto DiscountRule);
