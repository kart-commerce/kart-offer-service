using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.GetPromotionCampaignAdminView;

public sealed class GetPromotionCampaignAdminViewQueryHandler : IRequestHandler<GetPromotionCampaignAdminViewQuery, Result<PromotionCampaignAdminViewDto>>
{
    private readonly IPromotionCampaignRepository _campaigns;

    public GetPromotionCampaignAdminViewQueryHandler(IPromotionCampaignRepository campaigns)
    {
        _campaigns = campaigns;
    }

    public async Task<Result<PromotionCampaignAdminViewDto>> Handle(GetPromotionCampaignAdminViewQuery request, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetAsync(request.CampaignId, cancellationToken);
        return campaign is null
            ? Result.Failure<PromotionCampaignAdminViewDto>(Error.NotFound($"Promotion campaign '{request.CampaignId}' not found."))
            : Result.Success(PromotionCampaignAdminViewDto.FromDomain(campaign));
    }
}
