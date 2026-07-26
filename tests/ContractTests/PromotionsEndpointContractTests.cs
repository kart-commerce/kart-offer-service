using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KartOfferService.Application.Common.Models;
using KartOfferService.Domain.Promotions;
using Xunit;

namespace KartOfferService.ContractTests;

public class PromotionsEndpointContractTests : IClassFixture<OfferContractTestFactory>
{
    private readonly OfferContractTestFactory _factory;
    private readonly HttpClient _client;

    public PromotionsEndpointContractTests(OfferContractTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetActive_WithAnActiveCampaign_ReturnsIt()
    {
        var campaignId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _factory.Store.ActivePromotionReadModels.Add(new ActivePromotionReadModel(campaignId, now.AddDays(-1), now.AddDays(7)));

        var response = await _client.GetAsync("/v1/promotions/active");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().Should().Contain(e => e.GetProperty("campaignId").GetGuid() == campaignId);
    }

    [Fact]
    public async Task Create_AuthenticatedWithoutAdminRole_Returns403()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/promotions")
        {
            Content = JsonContent.Create(new
            {
                startsAt = DateTimeOffset.UtcNow,
                endsAt = DateTimeOffset.UtcNow.AddDays(7),
                discountRule = new { type = "percentageOff", value = 10 },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "customer");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WithAdminRole_Returns201_AndThenGetReturnsIt()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/promotions")
        {
            Content = JsonContent.Create(new
            {
                startsAt = DateTimeOffset.UtcNow,
                endsAt = DateTimeOffset.UtcNow.AddDays(7),
                discountRule = new { type = "percentageOff", value = 10 },
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var campaignId = created.GetProperty("campaignId").GetGuid();

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/promotions/{campaignId}");
        getRequest.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");
        var getResponse = await _client.SendAsync(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_WithStaleIfMatch_Returns412()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = PromotionCampaign.Create(now, now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", now).Value;
        _factory.Store.Campaigns[campaign.Id] = campaign;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/promotions/{campaign.Id}/deactivate");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", "999");
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be((HttpStatusCode)412);
    }

    [Fact]
    public async Task Deactivate_ForUnknownCampaign_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/promotions/{Guid.NewGuid()}/deactivate");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", "1");
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
