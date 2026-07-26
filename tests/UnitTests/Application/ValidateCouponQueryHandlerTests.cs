using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.ValidateCoupon;
using KartOfferService.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class ValidateCouponQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenCouponNotFound_ReturnsInvalidWithReason()
    {
        var reads = Substitute.For<ICouponReadRepository>();
        reads.GetAsync("MISSING", Arg.Any<CancellationToken>()).Returns((CouponReadModel?)null);
        var handler = new ValidateCouponQueryHandler(reads, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ValidateCouponQuery("MISSING", "user-1", new MoneyDto(10, "USD")), CancellationToken.None);

        response.Valid.Should().BeFalse();
        response.Reason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_WhenWithinWindowAndUnderCap_ReturnsValid()
    {
        var reads = Substitute.For<ICouponReadRepository>();
        reads.GetAsync("SAVE10", Arg.Any<CancellationToken>())
            .Returns(new CouponReadModel("SAVE10", null, 100, Now.AddDays(-1), Now.AddDays(30), 5));
        var handler = new ValidateCouponQueryHandler(reads, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ValidateCouponQuery("SAVE10", "user-1", new MoneyDto(10, "USD")), CancellationToken.None);

        response.Valid.Should().BeTrue();
        response.Reason.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenGlobalCapReached_ReturnsInvalid()
    {
        var reads = Substitute.For<ICouponReadRepository>();
        reads.GetAsync("SAVE10", Arg.Any<CancellationToken>())
            .Returns(new CouponReadModel("SAVE10", null, 10, Now.AddDays(-1), Now.AddDays(30), 10));
        var handler = new ValidateCouponQueryHandler(reads, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ValidateCouponQuery("SAVE10", "user-1", new MoneyDto(10, "USD")), CancellationToken.None);

        response.Valid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenOutsideValidityWindow_ReturnsInvalid()
    {
        var reads = Substitute.For<ICouponReadRepository>();
        reads.GetAsync("SAVE10", Arg.Any<CancellationToken>())
            .Returns(new CouponReadModel("SAVE10", null, null, Now.AddDays(1), Now.AddDays(30), 0));
        var handler = new ValidateCouponQueryHandler(reads, new FixedTimeProvider(Now));

        var response = await handler.Handle(new ValidateCouponQuery("SAVE10", "user-1", new MoneyDto(10, "USD")), CancellationToken.None);

        response.Valid.Should().BeFalse();
    }
}
