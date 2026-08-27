using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Domain.Coupons;
using MediatR;
using Microsoft.Extensions.Logging;

namespace KartOfferService.Application.Features.IssueCoupon;

/// <summary>api-contract.yaml: "`couponCode` already exists - no row written, no event published" - dedup is served by the natural-key PRIMARY KEY, not a separate idempotency ledger (design-decisions.md).</summary>
public sealed class IssueCouponCommandHandler : IRequestHandler<IssueCouponCommand, Result<CouponAdminViewDto>>
{
    private readonly ICouponRepository _coupons;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<IssueCouponCommandHandler> _logger;

    public IssueCouponCommandHandler(
        ICouponRepository coupons,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider,
        ILogger<IssueCouponCommandHandler> logger)
    {
        _coupons = coupons;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<Result<CouponAdminViewDto>> Handle(IssueCouponCommand request, CancellationToken cancellationToken)
    {
        var existing = await _coupons.GetAsync(request.CouponCode, cancellationToken);
        if (existing is not null)
        {
            _logger.LogWarning("Stage {Stage}: issue-coupon rejected, coupon code {CouponCode} already exists", "CouponCodeAlreadyExists", request.CouponCode);
            return Result.Failure<CouponAdminViewDto>(Error.Conflict($"Coupon code '{request.CouponCode}' already exists."));
        }

        var now = _timeProvider.GetUtcNow();
        var issueResult = Coupon.Issue(
            request.CouponCode, request.PerUserCap, request.GlobalCap, request.ValidFrom, request.ValidUntil,
            _currentPrincipal.ActingPrincipal, now);
        if (issueResult.IsFailure)
        {
            _logger.LogWarning(
                "Stage {Stage}: issue-coupon rejected for {CouponCode} - {ErrorCode}: {ErrorMessage}",
                "CouponIssueValidationFailed",
                request.CouponCode,
                issueResult.Error.Code,
                issueResult.Error.Message);
            return Result.Failure<CouponAdminViewDto>(issueResult.Error);
        }

        var coupon = issueResult.Value;
        await _coupons.AddAsync(coupon, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stage {Stage}: coupon {CouponCode} issued", "CouponIssuedStepCompleted", coupon.CouponCode);
        return Result.Success(CouponAdminViewDto.FromDomain(coupon));
    }
}
