using KartOfferService.Api.Common;
using KartOfferService.Api.Security;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.CreatePromotionCampaign;
using KartOfferService.Application.Features.DeactivatePromotionCampaign;
using KartOfferService.Application.Features.GetActivePromotions;
using KartOfferService.Application.Features.GetPromotionCampaignAdminView;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KartOfferService.Api.Controllers;

[ApiController]
[Route("v1/promotions")]
public sealed class PromotionsController : ControllerBase
{
    private readonly ISender _sender;

    public PromotionsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>OFF-7: api-contract.yaml `GET /v1/promotions/active` - the highest-QPS endpoint this service exposes; reads the MongoDB read model.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<ActivePromotionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ActivePromotionResponse>>> GetActive([FromQuery] string? sku, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(new GetActivePromotionsQuery(sku), cancellationToken);
        return Ok(response);
    }

    /// <summary>Not its own ticket - api-contract.yaml `GET /v1/promotions/{campaignId}` (admin-only), needed to obtain `version` for the deactivate `If-Match` precondition.</summary>
    [HttpGet("{campaignId:guid}")]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [ProducesResponseType(typeof(PromotionCampaignAdminViewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PromotionCampaignAdminViewDto>> GetAdminView([FromRoute] Guid campaignId, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetPromotionCampaignAdminViewQuery(campaignId), cancellationToken);
        return this.ToActionResult<PromotionCampaignAdminViewDto, PromotionCampaignAdminViewDto>(result, dto => Ok(dto));
    }

    /// <summary>OFF-5: api-contract.yaml `POST /v1/promotions` (admin-only, ADR-0019). Requires `Idempotency-Key`. Publishes `PromotionActivated`.</summary>
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
        var result = await _sender.Send(
            new CreatePromotionCampaignCommand(request.StartsAt, request.EndsAt, request.DiscountRule), cancellationToken);
        return this.ToActionResult<PromotionCampaignAdminViewDto, PromotionCampaignAdminViewDto>(
            result, dto => CreatedAtAction(nameof(GetAdminView), new { campaignId = dto.CampaignId }, dto));
    }

    /// <summary>OFF-6: api-contract.yaml `POST /v1/promotions/{campaignId}/deactivate` (admin-only). Requires `Idempotency-Key` + `If-Match`.</summary>
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
        var result = await _sender.Send(new DeactivatePromotionCampaignCommand(campaignId, ifMatch), cancellationToken);
        return this.ToActionResult<PromotionCampaignAdminViewDto, PromotionCampaignAdminViewDto>(result, dto => Ok(dto));
    }
}

/// <summary>api-contract.yaml `createPromotionCampaign` requestBody shape.</summary>
public sealed record CreatePromotionCampaignRequest(DateTimeOffset StartsAt, DateTimeOffset EndsAt, DiscountRuleDto DiscountRule);
