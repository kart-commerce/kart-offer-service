using Kart.Shared.Domain;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.GetPromotionCampaignAdminView;

/// <summary>api-contract.yaml `GET /v1/promotions/{campaignId}` - the symmetric counterpart to `GetCouponAdminView`, for the `If-Match`-gated deactivate flow.</summary>
public sealed record GetPromotionCampaignAdminViewQuery(Guid CampaignId) : IRequest<Result<PromotionCampaignAdminViewDto>>;
