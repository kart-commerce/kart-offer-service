using FluentAssertions;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Features.GetPricingQuote;
using KartOfferService.Domain.Pricing;
using KartOfferService.Domain.Promotions;
using KartOfferService.UnitTests.TestSupport;
using NSubstitute;
using Xunit;

namespace KartOfferService.UnitTests.Application;

public class GetPricingQuoteCommandHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly IProductPriceCache _priceCache = Substitute.For<IProductPriceCache>();
    private readonly IPromotionCampaignRepository _campaigns = Substitute.For<IPromotionCampaignRepository>();
    private readonly IPricingQuoteRepository _quotes = Substitute.For<IPricingQuoteRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentPrincipal _currentPrincipal = Substitute.For<ICurrentPrincipal>();

    private GetPricingQuoteCommandHandler CreateHandler() =>
        new(_priceCache, _campaigns, _quotes, _unitOfWork, _currentPrincipal, new FixedTimeProvider(Now));

    [Fact]
    public async Task Handle_WhenSkuHasNoKnownPrice_FailsValidation()
    {
        _priceCache.GetAsync("UNKNOWN-SKU", Arg.Any<CancellationToken>()).Returns((Money?)null);

        var result = await CreateHandler().Handle(
            new GetPricingQuoteCommand(new[] { new PricingLineItem("UNKNOWN-SKU", 1) }, "USD"), CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Fact]
    public async Task Handle_WithNoActiveCampaigns_QuotesRawSubtotal()
    {
        _priceCache.GetAsync("SKU-1", Arg.Any<CancellationToken>()).Returns(new Money(50, "USD"));
        _campaigns.GetActiveAsync(Now, Arg.Any<CancellationToken>()).Returns(Array.Empty<PromotionCampaign>());

        var result = await CreateHandler().Handle(
            new GetPricingQuoteCommand(new[] { new PricingLineItem("SKU-1", 2) }, "USD"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Amount.Should().Be(100);
        result.Value.ExpiresAt.Should().Be(Now.AddMinutes(15));
    }

    [Fact]
    public async Task Handle_WithActiveCampaign_AppliesBestDiscount()
    {
        _priceCache.GetAsync("SKU-1", Arg.Any<CancellationToken>()).Returns(new Money(100, "USD"));
        var campaign = PromotionCampaign.Create(Now.AddDays(-1), Now.AddDays(7), PercentageOffDiscountRule.Create(20).Value, "admin", Now.AddDays(-1)).Value;
        _campaigns.GetActiveAsync(Now, Arg.Any<CancellationToken>()).Returns(new[] { campaign });

        var result = await CreateHandler().Handle(
            new GetPricingQuoteCommand(new[] { new PricingLineItem("SKU-1", 1) }, "USD"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Total.Amount.Should().Be(80);
        await _quotes.Received(1).AddAsync(Arg.Any<PricingQuote>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithMultipleCampaigns_PicksLowestResultingTotal_NoStacking()
    {
        _priceCache.GetAsync("SKU-1", Arg.Any<CancellationToken>()).Returns(new Money(100, "USD"));
        var smallDiscount = PromotionCampaign.Create(Now.AddDays(-1), Now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", Now.AddDays(-1)).Value;
        var bigDiscount = PromotionCampaign.Create(Now.AddDays(-1), Now.AddDays(7), FixedAmountOffDiscountRule.Create(50).Value, "admin", Now.AddDays(-1)).Value;
        _campaigns.GetActiveAsync(Now, Arg.Any<CancellationToken>()).Returns(new[] { smallDiscount, bigDiscount });

        var result = await CreateHandler().Handle(
            new GetPricingQuoteCommand(new[] { new PricingLineItem("SKU-1", 1) }, "USD"), CancellationToken.None);

        result.Value.Total.Amount.Should().Be(50);
    }
}
