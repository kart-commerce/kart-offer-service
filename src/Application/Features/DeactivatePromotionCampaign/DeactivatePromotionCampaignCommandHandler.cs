using Kart.Shared.Domain;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Features.DeactivatePromotionCampaign;

/// <summary>
/// database-design.md's "Admin Write-Path Mechanics": the `version` last read via
/// `GET /v1/promotions/{campaignId}` must still match, or this fails 412 rather than silently
/// deactivating a campaign the caller no longer has an up-to-date view of. `Version` is also
/// mapped as an EF Core concurrency token (see PromotionCampaignConfiguration) - a race between
/// this check and the write itself is still caught at the database level.
/// </summary>
public sealed class DeactivatePromotionCampaignCommandHandler
    : IRequestHandler<DeactivatePromotionCampaignCommand, Result<PromotionCampaignAdminViewDto>>
{
    private readonly IPromotionCampaignRepository _campaigns;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeactivatePromotionCampaignCommandHandler> _logger;

    public DeactivatePromotionCampaignCommandHandler(
        IPromotionCampaignRepository campaigns,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<DeactivatePromotionCampaignCommandHandler> logger)
    {
        _campaigns = campaigns;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<PromotionCampaignAdminViewDto>> Handle(DeactivatePromotionCampaignCommand request, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetAsync(request.CampaignId, cancellationToken);
        if (campaign is null)
        {
            _logger.LogWarning("Stage {Stage}: deactivate-promotion-campaign rejected, campaign {CampaignId} not found", "PromotionCampaignNotFoundForDeactivate", request.CampaignId);
            return Result.Failure<PromotionCampaignAdminViewDto>(Error.NotFound($"Promotion campaign '{request.CampaignId}' not found."));
        }

        if (campaign.Version != request.ExpectedVersion)
        {
            _logger.LogWarning(
                "Stage {Stage}: deactivate-promotion-campaign rejected for {CampaignId}, expected version {ExpectedVersion} but current version is {CurrentVersion}",
                "PromotionCampaignDeactivateStaleVersion",
                request.CampaignId,
                request.ExpectedVersion,
                campaign.Version);
            return Result.Failure<PromotionCampaignAdminViewDto>(
                Error.Custom(ErrorCodes.StaleVersion, $"Expected version {request.ExpectedVersion} but current version is {campaign.Version}."));
        }

        campaign.Deactivate(_currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stage {Stage}: promotion campaign {CampaignId} deactivated", "PromotionCampaignDeactivatedStepCompleted", campaign.Id);
        return Result.Success(PromotionCampaignAdminViewDto.FromDomain(campaign));
    }
}
