using Kart.Shared.Domain;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MediatR;

namespace KartOfferService.Application.Features.DeactivateCoupon;

/// <summary>
/// edge-cases.md #7: shares <see cref="RedeemCoupon.RedeemCouponCommandHandler"/>'s per-`coupon_code`
/// `SELECT ... FOR UPDATE` lock, so an in-flight validate-then-redeem can never race a concurrent
/// admin deactivation into an inconsistent outcome.
/// </summary>
public sealed class DeactivateCouponCommandHandler : IRequestHandler<DeactivateCouponCommand, Result<CouponAdminViewDto>>
{
    private readonly ICouponRepository _coupons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public DeactivateCouponCommandHandler(
        ICouponRepository coupons,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider)
    {
        _coupons = coupons;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
    }

    public async Task<Result<CouponAdminViewDto>> Handle(DeactivateCouponCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var coupon = await _coupons.GetForUpdateAsync(request.CouponCode, cancellationToken);
            if (coupon is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CouponAdminViewDto>(Error.NotFound($"Coupon '{request.CouponCode}' not found."));
            }

            if (coupon.Version != request.ExpectedVersion)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure<CouponAdminViewDto>(
                    Error.Custom(ErrorCodes.StaleVersion, $"Expected version {request.ExpectedVersion} but current version is {coupon.Version}."));
            }

            coupon.Deactivate(_currentPrincipal.ActingPrincipal, _timeProvider.GetUtcNow());
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return Result.Success(CouponAdminViewDto.FromDomain(coupon));
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
