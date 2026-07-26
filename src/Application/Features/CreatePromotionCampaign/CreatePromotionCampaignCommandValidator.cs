using FluentValidation;
using KartOfferService.Application.Common.Models;

namespace KartOfferService.Application.Features.CreatePromotionCampaign;

public sealed class CreatePromotionCampaignCommandValidator : AbstractValidator<CreatePromotionCampaignCommand>
{
    public CreatePromotionCampaignCommandValidator()
    {
        RuleFor(x => x.EndsAt).GreaterThan(x => x.StartsAt);
        RuleFor(x => x.DiscountRule).NotNull();
        RuleFor(x => x.DiscountRule.Type)
            .Must(type => type is DiscountRuleDto.PercentageOffType or DiscountRuleDto.FixedAmountOffType)
            .WithMessage($"discountRule.type must be '{DiscountRuleDto.PercentageOffType}' or '{DiscountRuleDto.FixedAmountOffType}'.")
            .When(x => x.DiscountRule is not null);
        RuleFor(x => x.DiscountRule.Value).GreaterThan(0).When(x => x.DiscountRule is not null);
    }
}
