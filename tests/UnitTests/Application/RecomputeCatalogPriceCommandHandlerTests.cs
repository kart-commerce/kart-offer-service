using FluentAssertions;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.RecomputeCatalogPrice;
using KartOfferService.Domain.Pricing;
using KartOfferService.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class RecomputeCatalogPriceCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Handle_UpsertsPriceInDefaultCurrency()
    {
        var priceCache = Substitute.For<IProductPriceCache>();
        var handler = new RecomputeCatalogPriceCommandHandler(priceCache, new FixedTimeProvider(Now));

        var result = await handler.Handle(new RecomputeCatalogPriceCommand("SKU-1", 29.99m), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        await priceCache.Received(1).UpsertAsync(
            "SKU-1",
            Arg.Is<Money>(m => m.Amount == 29.99m && m.Currency == Constants.DefaultCurrency),
            Now,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNegativePrice_Fails_AndNeverUpserts()
    {
        var priceCache = Substitute.For<IProductPriceCache>();
        var handler = new RecomputeCatalogPriceCommandHandler(priceCache, new FixedTimeProvider(Now));

        var result = await handler.Handle(new RecomputeCatalogPriceCommand("SKU-1", -1), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        await priceCache.DidNotReceive().UpsertAsync(Arg.Any<string>(), Arg.Any<Money>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }
}
