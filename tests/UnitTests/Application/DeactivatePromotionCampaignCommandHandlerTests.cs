using FluentAssertions;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.DeactivatePromotionCampaign;
using KartOfferService.Domain.Promotions;
using KartOfferService.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class DeactivatePromotionCampaignCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IPromotionCampaignRepository _campaigns = Substitute.For<IPromotionCampaignRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentPrincipal _currentPrincipal = Substitute.For<ICurrentPrincipal>();

    private DeactivatePromotionCampaignCommandHandler CreateHandler() =>
        new(_campaigns, _unitOfWork, _currentPrincipal, new FixedTimeProvider(Now), NullLogger<DeactivatePromotionCampaignCommandHandler>.Instance);

    [Fact]
    public async Task Handle_WhenCampaignNotFound_ReturnsNotFound()
    {
        var campaignId = Guid.NewGuid();
        _campaigns.GetAsync(campaignId, Arg.Any<CancellationToken>()).Returns((PromotionCampaign?)null);

        var result = await CreateHandler().Handle(new DeactivatePromotionCampaignCommand(campaignId, 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("not_found");
    }

    [Fact]
    public async Task Handle_WhenVersionMismatch_ReturnsStaleVersion_AndNeverSaves()
    {
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", Now).Value;
        _campaigns.GetAsync(campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);

        var result = await CreateHandler().Handle(new DeactivatePromotionCampaignCommand(campaign.Id, campaign.Version + 1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ErrorCodes.StaleVersion);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMatchingVersion_Deactivates_AndSaves()
    {
        var campaign = PromotionCampaign.Create(Now, Now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", Now).Value;
        _campaigns.GetAsync(campaign.Id, Arg.Any<CancellationToken>()).Returns(campaign);

        var result = await CreateHandler().Handle(new DeactivatePromotionCampaignCommand(campaign.Id, campaign.Version), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        campaign.Window.EndsAt.Should().Be(Now);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
