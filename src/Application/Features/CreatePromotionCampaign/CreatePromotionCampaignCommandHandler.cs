using Kart.Shared.Domain;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Domain.Promotions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Features.CreatePromotionCampaign;

public sealed class CreatePromotionCampaignCommandHandler
    : IRequestHandler<CreatePromotionCampaignCommand, Result<PromotionCampaignAdminViewDto>>
{
    private readonly IPromotionCampaignRepository _campaigns;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<CreatePromotionCampaignCommandHandler> _logger;

    public CreatePromotionCampaignCommandHandler(
        IPromotionCampaignRepository campaigns,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<CreatePromotionCampaignCommandHandler> logger)
    {
        _campaigns = campaigns;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<PromotionCampaignAdminViewDto>> Handle(CreatePromotionCampaignCommand request, CancellationToken cancellationToken)
    {
        var ruleResult = ToDomainRule(request.DiscountRule);
        if (ruleResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: create-promotion-campaign rejected - {ErrorCode}: {ErrorMessage}",
                "PromotionCampaignDiscountRuleInvalid",
                ruleResult.Error.Code,
                ruleResult.Error.Message);
            return Result.Failure<PromotionCampaignAdminViewDto>(ruleResult.Error);
        }

        var now = _timeProvider.GetUtcNow();
        var creationResult = PromotionCampaign.Create(
            request.StartsAt, request.EndsAt, ruleResult.Value, _currentPrincipal.ActingPrincipal, now);
        if (creationResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: create-promotion-campaign rejected - {ErrorCode}: {ErrorMessage}",
                "PromotionCampaignCreateValidationFailed",
                creationResult.Error.Code,
                creationResult.Error.Message);
            return Result.Failure<PromotionCampaignAdminViewDto>(creationResult.Error);
        }

        var campaign = creationResult.Value;
        await _campaigns.AddAsync(campaign, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stage {Stage}: promotion campaign {CampaignId} created", "PromotionCampaignCreatedStepCompleted", campaign.Id);
        return Result.Success(PromotionCampaignAdminViewDto.FromDomain(campaign));
    }

    private static Result<DiscountRule> ToDomainRule(DiscountRuleDto dto) => dto.Type switch
    {
        DiscountRuleDto.PercentageOffType => PercentageOffDiscountRule.Create(dto.Value).Map(r => (DiscountRule)r),
        DiscountRuleDto.FixedAmountOffType => FixedAmountOffDiscountRule.Create(dto.Value).Map(r => (DiscountRule)r),
        _ => Result.Failure<DiscountRule>(Error.Validation($"Unknown discountRule.type '{dto.Type}'.")),
    };
}
