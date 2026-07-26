using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.GetCouponAdminView;
using KartOfferService.Domain.Coupons;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class GetCouponAdminViewQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenFound_ReturnsAdminView()
    {
        var coupons = Substitute.For<ICouponRepository>();
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        coupons.GetAsync("SAVE10", Arg.Any<CancellationToken>()).Returns(coupon);

        var result = await new GetCouponAdminViewQueryHandler(coupons).Handle(new GetCouponAdminViewQuery("SAVE10"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CouponCode.Should().Be("SAVE10");
        result.Value.Version.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        var coupons = Substitute.For<ICouponRepository>();
        coupons.GetAsync("MISSING", Arg.Any<CancellationToken>()).Returns((Coupon?)null);

        var result = await new GetCouponAdminViewQueryHandler(coupons).Handle(new GetCouponAdminViewQuery("MISSING"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }
}
