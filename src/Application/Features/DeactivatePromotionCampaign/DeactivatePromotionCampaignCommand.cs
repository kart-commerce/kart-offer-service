using Kart.Shared.Domain;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.DeactivatePromotionCampaign;

/// <summary>OFF-6: api-contract.yaml `POST /v1/promotions/{campaignId}/deactivate` - `ExpectedVersion` is the caller's `If-Match` header.</summary>
public sealed record DeactivatePromotionCampaignCommand(Guid CampaignId, int ExpectedVersion) : IRequest<Result<PromotionCampaignAdminViewDto>>;
