using Kart.Shared.Domain;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Domain.Promotions;
using MediatR;

namespace KartOfferService.Application.Features.CreatePromotionCampaign;

public sealed class CreatePromotionCampaignCommandHandler
    : IRequestHandler<CreatePromotionCampaignCommand, Result<PromotionCampaignAdminViewDto>>
{
    private readonly IPromotionCampaignRepository _campaigns;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public CreatePromotionCampaignCommandHandler(
        IPromotionCampaignRepository campaigns,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider)
    {
        _campaigns = campaigns;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PromotionCampaignAdminViewDto>> Handle(CreatePromotionCampaignCommand request, CancellationToken cancellationToken)
    {
        var ruleResult = ToDomainRule(request.DiscountRule);
        if (ruleResult.IsFailure)
        {
            return Result.Failure<PromotionCampaignAdminViewDto>(ruleResult.Error);
        }

        var now = _timeProvider.GetUtcNow();
        var creationResult = PromotionCampaign.Create(
            request.StartsAt, request.EndsAt, ruleResult.Value, _currentPrincipal.ActingPrincipal, now);
        if (creationResult.IsFailure)
        {
            return Result.Failure<PromotionCampaignAdminViewDto>(creationResult.Error);
        }

        var campaign = creationResult.Value;
        await _campaigns.AddAsync(campaign, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(PromotionCampaignAdminViewDto.FromDomain(campaign));
    }

    private static Result<DiscountRule> ToDomainRule(DiscountRuleDto dto) => dto.Type switch
    {
        DiscountRuleDto.PercentageOffType => PercentageOffDiscountRule.Create(dto.Value).Map(r => (DiscountRule)r),
        DiscountRuleDto.FixedAmountOffType => FixedAmountOffDiscountRule.Create(dto.Value).Map(r => (DiscountRule)r),
        _ => Result.Failure<DiscountRule>(Error.Validation($"Unknown discountRule.type '{dto.Type}'.")),
    };
}
