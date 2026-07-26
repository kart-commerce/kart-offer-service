using FluentValidation;

namespace KartOfferService.Application.Features.GetPricingQuote;

public sealed class GetPricingQuoteCommandValidator : AbstractValidator<GetPricingQuoteCommand>
{
    public GetPricingQuoteCommandValidator()
    {
        RuleFor(x => x.Currency).NotEmpty();
        RuleFor(x => x.Items).NotEmpty().WithMessage("At least one line item is required.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.Sku).NotEmpty();
            item.RuleFor(i => i.Quantity).GreaterThan(0);
        });
    }
}
