using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.GetCouponAdminView;

public sealed class GetCouponAdminViewQueryHandler : IRequestHandler<GetCouponAdminViewQuery, Result<CouponAdminViewDto>>
{
    private readonly ICouponRepository _coupons;

    public GetCouponAdminViewQueryHandler(ICouponRepository coupons)
    {
        _coupons = coupons;
    }

    public async Task<Result<CouponAdminViewDto>> Handle(GetCouponAdminViewQuery request, CancellationToken cancellationToken)
    {
        var coupon = await _coupons.GetAsync(request.CouponCode, cancellationToken);
        return coupon is null
            ? Result.Failure<CouponAdminViewDto>(Error.NotFound($"Coupon '{request.CouponCode}' not found."))
            : Result.Success(CouponAdminViewDto.FromDomain(coupon));
    }
}
