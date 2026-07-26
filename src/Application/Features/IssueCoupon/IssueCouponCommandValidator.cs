using FluentValidation;

namespace KartOfferService.Application.Features.IssueCoupon;

public sealed class IssueCouponCommandValidator : AbstractValidator<IssueCouponCommand>
{
    public IssueCouponCommandValidator()
    {
        RuleFor(x => x.CouponCode).NotEmpty();
        RuleFor(x => x.ValidUntil).GreaterThan(x => x.ValidFrom);
        RuleFor(x => x.PerUserCap).GreaterThan(0).When(x => x.PerUserCap is not null);
        RuleFor(x => x.GlobalCap).GreaterThan(0).When(x => x.GlobalCap is not null);
    }
}
