using FluentValidation;

namespace KartOfferService.Application.Features.ValidateCoupon;

public sealed class ValidateCouponQueryValidator : AbstractValidator<ValidateCouponQuery>
{
    public ValidateCouponQueryValidator()
    {
        RuleFor(x => x.CouponCode).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CartTotal).NotNull();
        RuleFor(x => x.CartTotal.Amount).GreaterThanOrEqualTo(0).When(x => x.CartTotal is not null);
        RuleFor(x => x.CartTotal.Currency).NotEmpty().When(x => x.CartTotal is not null);
    }
}
