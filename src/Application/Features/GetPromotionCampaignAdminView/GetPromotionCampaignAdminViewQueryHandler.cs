using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Features.GetPromotionCampaignAdminView;

public sealed class GetPromotionCampaignAdminViewQueryHandler : IRequestHandler<GetPromotionCampaignAdminViewQuery, Result<PromotionCampaignAdminViewDto>>
{
    private readonly IPromotionCampaignRepository _campaigns;
    private readonly ILogger<GetPromotionCampaignAdminViewQueryHandler> _logger;

    public GetPromotionCampaignAdminViewQueryHandler(IPromotionCampaignRepository campaigns, ILogger<GetPromotionCampaignAdminViewQueryHandler> logger)
    {
        _campaigns = campaigns;
        _logger = logger;
    }

    public async Task<Result<PromotionCampaignAdminViewDto>> Handle(GetPromotionCampaignAdminViewQuery request, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetAsync(request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            _logger.LogWarning("Stage {Stage}: get-promotion-campaign-admin-view rejected, campaign {CampaignId} not found", "PromotionCampaignNotFoundForAdminView", request.CampaignId);
            return Result.Failure<PromotionCampaignAdminViewDto>(Error.NotFound($"Promotion campaign '{request.CampaignId}' not found."));
        }

        return Result.Success(PromotionCampaignAdminViewDto.FromDomain(campaign));
    }
}
