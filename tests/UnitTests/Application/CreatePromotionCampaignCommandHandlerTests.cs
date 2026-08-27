using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.CreatePromotionCampaign;
using KartOfferService.Domain.Promotions;
using KartOfferService.UnitTests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class CreatePromotionCampaignCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IPromotionCampaignRepository _campaigns = Substitute.For<IPromotionCampaignRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentPrincipal _currentPrincipal = Substitute.For<ICurrentPrincipal>();

    private CreatePromotionCampaignCommandHandler CreateHandler() =>
        new(_campaigns, _unitOfWork, _currentPrincipal, new FixedTimeProvider(Now), NullLogger<CreatePromotionCampaignCommandHandler>.Instance);

    [Fact]
    public async Task Handle_WithPercentageOffRule_CreatesCampaign_AndSaves()
    {
        _currentPrincipal.ActingPrincipal.Returns("admin");

        var result = await CreateHandler().Handle(
            new CreatePromotionCampaignCommand(Now, Now.AddDays(7), new DiscountRuleDto(DiscountRuleDto.PercentageOffType, 10)),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.DiscountRule.Type.Should().Be(DiscountRuleDto.PercentageOffType);
        await _campaigns.Received(1).AddAsync(Arg.Any<PromotionCampaign>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidWindow_Fails_AndNeverSaves()
    {
        var result = await CreateHandler().Handle(
            new CreatePromotionCampaignCommand(Now, Now, new DiscountRuleDto(DiscountRuleDto.PercentageOffType, 10)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithInvalidDiscountRuleValue_Fails()
    {
        var result = await CreateHandler().Handle(
            new CreatePromotionCampaignCommand(Now, Now.AddDays(7), new DiscountRuleDto(DiscountRuleDto.PercentageOffType, 150)),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }
}
