using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KartOfferService.Domain.Pricing;
using Xunit;

namespace KartOfferService.ContractTests;

public class PricingEndpointContractTests : IClassFixture<OfferContractTestFactory>
{
    private readonly OfferContractTestFactory _factory;
    private readonly HttpClient _client;

    public PricingEndpointContractTests(OfferContractTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetQuote_ForUnknownSku_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/v1/pricing/quote", new
        {
            items = new[] { new { sku = "UNKNOWN-SKU", quantity = 1 } },
            currency = "USD",
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetQuote_ForKnownSku_Returns200WithQuoteIdAndExpiry()
    {
        _factory.Store.ProductPrices["SKU-1"] = new Money(25, "USD");

        var response = await _client.PostAsJsonAsync("/v1/pricing/quote", new
        {
            items = new[] { new { sku = "SKU-1", quantity = 2 } },
            currency = "USD",
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();
        quote.GetProperty("total").GetProperty("amount").GetDecimal().Should().Be(50);
        quote.GetProperty("expiresAt").GetDateTimeOffset().Should().BeAfter(DateTimeOffset.UtcNow);
    }
}
