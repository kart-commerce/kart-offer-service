using FluentValidation;

namespace KartOfferService.Application.Features.VoidCouponRedemption;

public sealed class VoidCouponRedemptionCommandValidator : AbstractValidator<VoidCouponRedemptionCommand>
{
    public VoidCouponRedemptionCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
