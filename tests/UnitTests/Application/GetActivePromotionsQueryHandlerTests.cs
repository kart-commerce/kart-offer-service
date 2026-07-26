using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Application.Features.GetActivePromotions;
using KartOfferService.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class GetActivePromotionsQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_MapsReadModelToResponse()
    {
        var campaignId = Guid.NewGuid();
        var reads = Substitute.For<IPromotionReadRepository>();
        reads.GetActiveAsync(Now, Arg.Any<CancellationToken>())
            .Returns(new[] { new ActivePromotionReadModel(campaignId, Now.AddDays(-1), Now.AddDays(7)) });
        var handler = new GetActivePromotionsQueryHandler(reads, new FixedTimeProvider(Now));

        var response = await handler.Handle(new GetActivePromotionsQuery(null), CancellationToken.None);

        response.Should().ContainSingle();
        response[0].CampaignId.Should().Be(campaignId);
        response[0].Window.StartsAt.Should().Be(Now.AddDays(-1));
        response[0].Window.EndsAt.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public async Task Handle_WhenNoneActive_ReturnsEmptyList()
    {
        var reads = Substitute.For<IPromotionReadRepository>();
        reads.GetActiveAsync(Now, Arg.Any<CancellationToken>()).Returns(Array.Empty<ActivePromotionReadModel>());
        var handler = new GetActivePromotionsQueryHandler(reads, new FixedTimeProvider(Now));

        var response = await handler.Handle(new GetActivePromotionsQuery("some-sku"), CancellationToken.None);

        response.Should().BeEmpty();
    }
}
