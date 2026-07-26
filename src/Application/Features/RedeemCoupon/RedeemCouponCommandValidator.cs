using FluentValidation;

namespace KartOfferService.Application.Features.RedeemCoupon;

public sealed class RedeemCouponCommandValidator : AbstractValidator<RedeemCouponCommand>
{
    public RedeemCouponCommandValidator()
    {
        RuleFor(x => x.CouponCode).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
