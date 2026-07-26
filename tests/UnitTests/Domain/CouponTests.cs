using FluentAssertions;
using KartOfferService.Domain.Coupons;
using Xunit;

namespace KartOfferService.UnitTests.Domain;

public class CouponTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_WithValidInput_RaisesCouponIssuedDomainEvent()
    {
        var result = Coupon.Issue("SAVE10", perUserCap: 1, globalCap: 100, Now, Now.AddDays(30), "admin", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CouponIssuedDomainEvent>();
        result.Value.CouponCode.Should().Be("SAVE10");
        result.Value.Version.Should().Be(1);
    }

    [Fact]
    public void Issue_NormalizesCouponCodeToUppercaseAndTrimmed()
    {
        var result = Coupon.Issue("  save10  ", null, null, Now, Now.AddDays(30), "admin", Now);

        result.Value.CouponCode.Should().Be("SAVE10");
    }

    [Fact]
    public void Issue_WhenValidUntilIsNotAfterValidFrom_Fails()
    {
        var result = Coupon.Issue("SAVE10", null, null, Now, Now, "admin", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void Redeem_WithinWindowAndUnderCaps_Succeeds_IncrementsTotalRedemptions_AndRaisesEvent()
    {
        var coupon = Coupon.Issue("SAVE10", perUserCap: 2, globalCap: 10, Now, Now.AddDays(30), "admin", Now).Value;
        coupon.ClearDomainEvents();

        var result = coupon.Redeem("user-1", "order-1", userRedemptionsSoFar: 0, "user-1", Now.AddDays(1));

        result.IsSuccess.Should().BeTrue();
        coupon.TotalRedemptions.Should().Be(1);
        coupon.Version.Should().Be(2);
        coupon.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CouponRedeemedDomainEvent>();
    }

    [Fact]
    public void Redeem_BeforeValidFrom_FailsWithCouponExpired()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now.AddDays(10), Now.AddDays(30), "admin", Now).Value;

        var result = coupon.Redeem("user-1", "order-1", 0, "user-1", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("coupon_expired");
    }

    [Fact]
    public void Redeem_AfterValidUntil_FailsWithCouponExpired()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(1), "admin", Now).Value;

        var result = coupon.Redeem("user-1", "order-1", 0, "user-1", Now.AddDays(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("coupon_expired");
    }

    [Fact]
    public void Redeem_AtGlobalCap_FailsWithConflict()
    {
        var coupon = Coupon.Issue("SAVE10", null, globalCap: 1, Now, Now.AddDays(30), "admin", Now).Value;
        coupon.Redeem("user-1", "order-1", 0, "user-1", Now.AddHours(1));

        var result = coupon.Redeem("user-2", "order-2", 0, "user-2", Now.AddHours(2));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("conflict");
    }

    [Fact]
    public void Redeem_AtPerUserCap_FailsWithConflict()
    {
        var coupon = Coupon.Issue("SAVE10", perUserCap: 1, null, Now, Now.AddDays(30), "admin", Now).Value;

        var result = coupon.Redeem("user-1", "order-1", userRedemptionsSoFar: 1, "user-1", Now.AddHours(1));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("conflict");
    }

    [Fact]
    public void VoidRedemption_DecrementsTotalRedemptions_AndRaisesEvent()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        coupon.Redeem("user-1", "order-1", 0, "user-1", Now.AddHours(1));
        coupon.ClearDomainEvents();

        coupon.VoidRedemption("order-1", "system:order-events-consumer", Now.AddHours(2));

        coupon.TotalRedemptions.Should().Be(0);
        coupon.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CouponRedemptionVoidedDomainEvent>();
    }

    [Fact]
    public void VoidRedemption_NeverGoesNegative()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;

        coupon.VoidRedemption("order-1", "system:order-events-consumer", Now.AddHours(2));

        coupon.TotalRedemptions.Should().Be(0);
    }

    [Fact]
    public void Deactivate_BeforeNaturalExpiry_TruncatesValidUntil_AndRaisesEvent()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        coupon.ClearDomainEvents();

        coupon.Deactivate("admin", Now.AddDays(1));

        coupon.ValidUntil.Should().Be(Now.AddDays(1));
        coupon.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<CouponDeactivatedDomainEvent>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyExpired_IsIdempotent_AndRaisesNoEvent()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(1), "admin", Now).Value;
        coupon.Deactivate("admin", Now.AddDays(2));
        coupon.ClearDomainEvents();

        coupon.Deactivate("admin", Now.AddDays(3));

        coupon.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CanRedeem_DoesNotMutateStateOrRaiseEvents()
    {
        var coupon = Coupon.Issue("SAVE10", null, null, Now, Now.AddDays(30), "admin", Now).Value;
        coupon.ClearDomainEvents();

        var result = coupon.CanRedeem(userRedemptionsSoFar: 0, Now.AddDays(1));

        result.IsSuccess.Should().BeTrue();
        coupon.TotalRedemptions.Should().Be(0);
        coupon.DomainEvents.Should().BeEmpty();
    }
}
