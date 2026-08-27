using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Features.VoidCouponRedemption;

public sealed class VoidCouponRedemptionCommandHandler : IRequestHandler<VoidCouponRedemptionCommand, Result>
{
    private readonly ICouponRepository _coupons;
    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VoidCouponRedemptionCommandHandler> _logger;

    public VoidCouponRedemptionCommandHandler(
        ICouponRepository coupons,
        ICouponRedemptionRepository redemptions,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        ILogger<VoidCouponRedemptionCommandHandler> logger)
    {
        _coupons = coupons;
        _redemptions = redemptions;
        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(VoidCouponRedemptionCommand request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        const string actingPrincipal = Common.SystemPrincipals.OrderEventsConsumer;

        var activeRedemptions = await _redemptions.GetActiveByOrderIdAsync(request.OrderId, cancellationToken);
        if (activeRedemptions.Count == 0)
        {
            // No coupon was ever redeemed against this order - a no-op, not an error.
            _logger.LogInformation(
                "Stage {Stage}: void-coupon-redemption for order {OrderId} is a no-op - no active redemptions found",
                "CouponRedemptionVoidNoOpNoActiveRedemptionsBranch",
                request.OrderId);
            return Result.Success();
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var redemption in activeRedemptions)
            {
                var coupon = await _coupons.GetForUpdateAsync(redemption.CouponCode, cancellationToken);
                if (coupon is null)
                {
                    continue;
                }

                redemption.Void(now, actingPrincipal);
                coupon.VoidRedemption(redemption.OrderId, actingPrincipal, now);
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.LogInformation(
                "Stage {Stage}: {RedemptionCount} coupon redemption(s) voided for order {OrderId}",
                "CouponRedemptionVoidedStepCompleted",
                activeRedemptions.Count,
                request.OrderId);
            return Result.Success();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
