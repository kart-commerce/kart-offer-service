using FluentValidation;

namespace KartOfferService.Application.Features.RecomputeCatalogPrice;

public sealed class RecomputeCatalogPriceCommandValidator : AbstractValidator<RecomputeCatalogPriceCommand>
{
    public RecomputeCatalogPriceCommandValidator()
    {
        RuleFor(x => x.Sku).NotEmpty();
        RuleFor(x => x.NewPrice).GreaterThanOrEqualTo(0);
    }
}
