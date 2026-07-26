using Kart.Shared.Domain;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.CreatePromotionCampaign;

/// <summary>OFF-5: api-contract.yaml `POST /v1/promotions` (admin-only, ADR-0019). Publishes `PromotionActivated`.</summary>
public sealed record CreatePromotionCampaignCommand(
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DiscountRuleDto DiscountRule) : IRequest<Result<PromotionCampaignAdminViewDto>>;
