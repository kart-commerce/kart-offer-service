using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.IssueCoupon;
using KartOfferService.Domain.Coupons;
using KartOfferService.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class IssueCouponCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly ICouponRepository _coupons = Substitute.For<ICouponRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentPrincipal _currentPrincipal = Substitute.For<ICurrentPrincipal>();

    private IssueCouponCommandHandler CreateHandler() =>
        new(_coupons, _unitOfWork, _currentPrincipal, new FixedTimeProvider(Now), NullLogger<IssueCouponCommandHandler>.Instance);

    [Fact]
    public async Task Handle_WhenCouponCodeAlreadyExists_ReturnsConflict_AndNeverSaves()
    {
        var existing = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        _coupons.GetAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().Handle(
            new IssueCouponCommand("SAVE10", null, null, Now, Now.AddDays(30)), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("conflict");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNew_IssuesCoupon_AndSaves()
    {
        _coupons.GetAsync("SAVE10", Arg.Any<CancellationToken>()).Returns((Coupon?)null);
        _currentPrincipal.ActingPrincipal.Returns("admin");

        var result = await CreateHandler().Handle(
            new IssueCouponCommand("SAVE10", 1, 100, Now, Now.AddDays(30)), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CouponCode.Should().Be("SAVE10");
        await _coupons.Received(1).AddAsync(Arg.Any<Coupon>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
