using FluentAssertions;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.DeactivateCoupon;
using KartOfferService.Domain.Coupons;
using KartOfferService.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class DeactivateCouponCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ICouponRepository _coupons = Substitute.For<ICouponRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentPrincipal _currentPrincipal = Substitute.For<ICurrentPrincipal>();

    private DeactivateCouponCommandHandler CreateHandler() =>
        new(_coupons, _unitOfWork, _currentPrincipal, new FixedTimeProvider(Now), NullLogger<DeactivateCouponCommandHandler>.Instance);

    [Fact]
    public async Task Handle_WhenCouponNotFound_RollsBack_AndReturnsNotFound()
    {
        _coupons.GetForUpdateAsync("MISSING", Arg.Any<CancellationToken>()).Returns((Coupon?)null);

        var result = await CreateHandler().Handle(new DeactivateCouponCommand("MISSING", 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenVersionMismatch_RollsBack_AndReturnsStaleVersion()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        _coupons.GetForUpdateAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await CreateHandler().Handle(new DeactivateCouponCommand("SAVE10", coupon.Version + 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.StaleVersion);
        await _unitOfWork.Received(1).RollbackTransactionAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMatchingVersion_Deactivates_AndCommits()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        _coupons.GetForUpdateAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await CreateHandler().Handle(new DeactivateCouponCommand("SAVE10", coupon.Version), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        coupon.ValidUntil.Should().Be(Now);
        await _unitOfWork.Received(1).CommitTransactionAsync(Arg.Any<CancellationToken>());
    }
}
