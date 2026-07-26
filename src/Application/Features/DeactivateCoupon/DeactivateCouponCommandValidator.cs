using FluentValidation;

namespace KartOfferService.Application.Features.DeactivateCoupon;

public sealed class DeactivateCouponCommandValidator : AbstractValidator<DeactivateCouponCommand>
{
    public DeactivateCouponCommandValidator()
    {
        RuleFor(x => x.CouponCode).NotEmpty();
        RuleFor(x => x.ExpectedVersion).GreaterThan(0);
    }
}
