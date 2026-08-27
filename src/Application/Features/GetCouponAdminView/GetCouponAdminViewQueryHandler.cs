using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Features.GetCouponAdminView;

public sealed class GetCouponAdminViewQueryHandler : IRequestHandler<GetCouponAdminViewQuery, Result<CouponAdminViewDto>>
{
    private readonly ICouponRepository _coupons;
    private readonly ILogger<GetCouponAdminViewQueryHandler> _logger;

    public GetCouponAdminViewQueryHandler(ICouponRepository coupons, ILogger<GetCouponAdminViewQueryHandler> logger)
    {
        _coupons = coupons;
        _logger = logger;
    }

    public async Task<Result<CouponAdminViewDto>> Handle(GetCouponAdminViewQuery request, CancellationToken cancellationToken)
    {
        var coupon = await _coupons.GetAsync(request.CouponCode, cancellationToken);
        if (coupon is null)
        {
            _logger.LogWarning("Stage {Stage}: get-coupon-admin-view rejected, coupon {CouponCode} not found", "CouponNotFoundForAdminView", request.CouponCode);
            return Result.Failure<CouponAdminViewDto>(Error.NotFound($"Coupon '{request.CouponCode}' not found."));
        }

        return Result.Success(CouponAdminViewDto.FromDomain(coupon));
    }
}
