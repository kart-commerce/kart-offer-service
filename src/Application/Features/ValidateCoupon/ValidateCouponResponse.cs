namespace KartOfferService.Application.Features.ValidateCoupon;

public sealed record ValidateCouponResponse(bool Valid, string? Reason);
