using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.RedeemCoupon;
using KartOfferService.Domain.Coupons;
using KartOfferService.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class RedeemCouponCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ICouponRepository _coupons = Substitute.For<ICouponRepository>();
    private readonly ICouponRedemptionRepository _redemptions = Substitute.For<ICouponRedemptionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentPrincipal _currentPrincipal = Substitute.For<ICurrentPrincipal>();

    private RedeemCouponCommandHandler CreateHandler() =>
        new(_coupons, _redemptions, _unitOfWork, _currentPrincipal, new FixedTimeProvider(Now));

    [Fact]
    public async Task Handle_WhenAlreadyRedeemedForSameOrder_IsIdempotent_AndNeverLocksOrWrites()
    {
        _redemptions.GetAsync("SAVE10", "order-1", Arg.Any<CancellationToken>())
            .Returns(CallCouponRedemptionFactory());

        var result = await CreateHandler().Handle(new RedeemCouponCommand("SAVE10", "user-1", "order-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _coupons.DidNotReceive().GetForUpdateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCouponDoesNotExist_ReturnsNotFound()
    {
        _redemptions.GetAsync("MISSING", "order-1", Arg.Any<CancellationToken>()).Returns((CouponRedemption?)null);
        _coupons.GetForUpdateAsync("MISSING", Arg.Any<CancellationToken>()).Returns((Coupon?)null);
        _currentPrincipal.ActingPrincipal.Returns("user-1");

        var result = await CreateHandler().Handle(new RedeemCouponCommand("MISSING", "user-1", "order-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WhenRedeemable_AddsRedemption_AndCommits()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now.AddDays(-1), Now.AddDays(30), "admin", Now.AddDays(-1)).Value;
        _redemptions.GetAsync("SAVE10", "order-1", Arg.Any<CancellationToken>()).Returns((CouponRedemption?)null);
        _coupons.GetForUpdateAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);
        _redemptions.CountActiveByUserAsync("SAVE10", "user-1", Arg.Any<CancellationToken>()).Returns(0);
        _currentPrincipal.ActingPrincipal.Returns("user-1");

        var result = await CreateHandler().Handle(new RedeemCouponCommand("SAVE10", "user-1", "order-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        coupon.TotalRedemptions.Should().Be(1);
        await _redemptions.Received(1).AddAsync(Arg.Any<CouponRedemption>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCapExceeded_RollsBack_AndReturnsConflict()
    {
        var coupon = Coupon.Issue("SAVE10", perUserCap: 1, null, Now.AddDays(-1), Now.AddDays(30), "admin", Now.AddDays(-1)).Value;
        _redemptions.GetAsync("SAVE10", "order-1", Arg.Any<CancellationToken>()).Returns((CouponRedemption?)null);
        _coupons.GetForUpdateAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);
        _redemptions.CountActiveByUserAsync("SAVE10", "user-1", Arg.Any<CancellationToken>()).Returns(1);

        var result = await CreateHandler().Handle(new RedeemCouponCommand("SAVE10", "user-1", "order-1"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("conflict");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
        await _redemptions.DidNotReceive().AddAsync(Arg.Any<CouponRedemption>(), Arg.Any<CancellationToken>());
    }

    private static CouponRedemption CallCouponRedemptionFactory()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now.AddDays(-1), Now.AddDays(30), "admin", Now.AddDays(-1)).Value;
        return coupon.Redeem("user-1", "order-1", 0, "user-1", Now).Value;
    }
}
