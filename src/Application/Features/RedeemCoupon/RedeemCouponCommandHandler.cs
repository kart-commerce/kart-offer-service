using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using MediatR;

namespace KartOfferService.Application.Features.RedeemCoupon;

/// <summary>
/// design-decisions.md "Concurrency Control for Coupon": a single per-`coupon_code` `SELECT ...
/// FOR UPDATE` covers the redemption, cap-check, and validity-window re-check as one atomic unit -
/// reused by <see cref="DeactivateCoupon.DeactivateCouponCommandHandler"/>'s own race (edge-cases.md #7).
/// </summary>
public sealed class RedeemCouponCommandHandler : IRequestHandler<RedeemCouponCommand, Result>
{
    private readonly ICouponRepository _coupons;
    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public RedeemCouponCommandHandler(
        ICouponRepository coupons,
        ICouponRedemptionRepository redemptions,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider)
    {
        _coupons = coupons;
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(RedeemCouponCommand request, CancellationToken cancellationToken)
    {
        // edge-cases.md #1: (coupon_code, order_id) is already unique in the database - a retried
        // Idempotency-Key resubmission for the same pair is served here as an idempotent success,
        // never a second redemption attempt.
        var existing = await _redemptions.GetAsync(request.CouponCode, request.OrderId, cancellationToken);
        if (existing is not null)
        {
            return Result.Success();
        }

        var now = _timeProvider.GetUtcNow();
        var actingPrincipal = _currentPrincipal.ActingPrincipal;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var coupon = await _coupons.GetForUpdateAsync(request.CouponCode, cancellationToken);
            if (coupon is null)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(Error.NotFound($"Coupon '{request.CouponCode}' not found."));
            }

            var userRedemptionsSoFar = await _redemptions.CountActiveByUserAsync(request.CouponCode, request.UserId, cancellationToken);
            var redeemResult = coupon.Redeem(request.UserId, request.OrderId, userRedemptionsSoFar, actingPrincipal, now);
            if (redeemResult.IsFailure)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Result.Failure(redeemResult.Error);
            }

            await _redemptions.AddAsync(redeemResult.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return Result.Success();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
