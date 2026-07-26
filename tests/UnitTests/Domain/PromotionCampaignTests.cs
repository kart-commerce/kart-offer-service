using FluentAssertions;
using KartOfferService.Domain.Promotions;
using Xunit;

namespace KartOfferService.UnitTests.Domain;

public class PromotionCampaignTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static DiscountRule SomeRule() => PercentageOffDiscountRule.Create(10).Value;

    [Fact]
    public void Create_WithValidWindow_RaisesPromotionActivatedDomainEvent()
    {
        var result = PromotionCampaign.Create(Now, Now.AddDays(7), SomeRule(), "admin", Now);

        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(1);
        result.Value.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PromotionActivatedDomainEvent>();
    }

    [Fact]
    public void Create_WhenEndsAtIsNotAfterStartsAt_Fails()
    {
        var result = PromotionCampaign.Create(Now, Now, SomeRule(), "admin", Now);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public void IsActive_WithinWindow_IsTrue()
    {
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(7), SomeRule(), "admin", Now).Value;

        campaign.IsActive(Now.AddDays(1)).Should().BeTrue();
    }

    [Fact]
    public void IsActive_OutsideWindow_IsFalse()
    {
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(7), SomeRule(), "admin", Now).Value;

        campaign.IsActive(Now.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void Deactivate_BeforeNaturalEnd_TruncatesWindow_AndRaisesEvent()
    {
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(7), SomeRule(), "admin", Now).Value;
        campaign.ClearDomainEvents();

        campaign.Deactivate("admin", Now.AddDays(2));

        campaign.Window.EndsAt.Should().Be(Now.AddDays(2));
        campaign.Version.Should().Be(2);
        campaign.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PromotionDeactivatedDomainEvent>();
    }

    [Fact]
    public void Deactivate_WhenAlreadyEnded_IsIdempotent_AndRaisesNoEvent()
    {
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(1), SomeRule(), "admin", Now).Value;
        campaign.Deactivate("admin", Now.AddDays(2));
        campaign.ClearDomainEvents();

        campaign.Deactivate("admin", Now.AddDays(3));

        campaign.DomainEvents.Should().BeEmpty();
    }
}
