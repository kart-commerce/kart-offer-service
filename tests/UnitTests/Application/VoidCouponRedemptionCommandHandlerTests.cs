using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.VoidCouponRedemption;
using KartOfferService.Domain.Coupons;
using KartOfferService.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class VoidCouponRedemptionCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ICouponRepository _coupons = Substitute.For<ICouponRepository>();
    private readonly ICouponRedemptionRepository _redemptions = Substitute.For<ICouponRedemptionRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private VoidCouponRedemptionCommandHandler CreateHandler() =>
        new(_coupons, _redemptions, _unitOfWork, new FixedTimeProvider(Now), NullLogger<VoidCouponRedemptionCommandHandler>.Instance);

    [Fact]
    public async Task Handle_WhenNoRedemptionsForOrder_IsNoOp_AndNeverOpensTransaction()
    {
        _redemptions.GetActiveByOrderIdAsync("order-1", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<CouponRedemption>());

        var result = await CreateHandler().Handle(new VoidCouponRedemptionCommand("order-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await _unitOfWork.DidNotReceive().BeginTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenRedemptionExists_VoidsRedemption_AndDecrementsCoupon()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now.AddDays(-2), Now.AddDays(30), "admin", Now.AddDays(-2)).Value;
        var redemption = coupon.Redeem("user-1", "order-1", 0, "user-1", Now.AddDays(-1)).Value;

        _redemptions.GetActiveByOrderIdAsync("order-1", Arg.Any<CancellationToken>()).Returns(new[] { redemption });
        _coupons.GetForUpdateAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await CreateHandler().Handle(new VoidCouponRedemptionCommand("order-1"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        redemption.VoidedAt.Should().NotBeNull();
        coupon.TotalRedemptions.Should().Be(0);
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }
}
