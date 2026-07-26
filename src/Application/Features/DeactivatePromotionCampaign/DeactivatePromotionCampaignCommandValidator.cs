using FluentValidation;

namespace KartOfferService.Application.Features.DeactivatePromotionCampaign;

public sealed class DeactivatePromotionCampaignCommandValidator : AbstractValidator<DeactivatePromotionCampaignCommand>
{
    public DeactivatePromotionCampaignCommandValidator()
    {
        RuleFor(x => x.CampaignId).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
