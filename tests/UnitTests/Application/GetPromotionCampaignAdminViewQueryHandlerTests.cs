using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.GetPromotionCampaignAdminView;
using KartOfferService.Domain.Promotions;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class GetPromotionCampaignAdminViewQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_WhenFound_ReturnsAdminView()
    {
        var campaigns = Substitute.For<IPromotionCampaignRepository>();
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", Now).Value;
        campaigns.GetAsync(campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);

        var result = await new GetPromotionCampaignAdminViewQueryHandler(campaigns)
            .Handle(new GetPromotionCampaignAdminViewQuery(campaign.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.CampaignId.Should().Be(campaign.Id);
    }

    [Fact]
    public async Task Handle_WhenNotFound_ReturnsNotFound()
    {
        var campaigns = Substitute.For<IPromotionCampaignRepository>();
        var campaignId = Guid.NewGuid();
        campaigns.GetAsync(campaignId, Arg.Any<CancellationToken>()).Returns((PromotionCampaign?)null);

        var result = await new GetPromotionCampaignAdminViewQueryHandler(campaigns)
            .Handle(new GetPromotionCampaignAdminViewQuery(campaignId), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }
}
