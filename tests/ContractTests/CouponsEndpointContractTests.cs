using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using KartOfferService.Domain.Coupons;
using Xunit;

namespace KartOfferService.ContractTests;

public class CouponsEndpointContractTests : IClassFixture<OfferContractTestFactory>
{
    private readonly OfferContractTestFactory _factory;
    private readonly HttpClient _client;

    public CouponsEndpointContractTests(OfferContractTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Validate_ForUnknownCoupon_Returns200WithValidFalse()
    {
        var response = await _client.PostAsJsonAsync("/v1/coupons/validate", new
        {
            couponCode = "UNKNOWN",
            userId = "user-1",
            cartTotal = new { amount = 50, currency = "USD" },
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("valid").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Redeem_ForUnknownCoupon_Returns404()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/coupons/redeem")
        {
            Content = JsonContent.Create(new { couponCode = "UNKNOWN", userId = "user-1", orderId = "order-1" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Issue_AuthenticatedWithoutAdminRole_Returns403()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/coupons")
        {
            Content = JsonContent.Create(new
            {
                couponCode = "NOADMIN",
                validFrom = DateTimeOffset.UtcNow,
                validUntil = DateTimeOffset.UtcNow.AddDays(30),
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "customer");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Issue_Unauthenticated_Returns401()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/coupons")
        {
            Content = JsonContent.Create(new
            {
                couponCode = "NOAUTH",
                validFrom = DateTimeOffset.UtcNow,
                validUntil = DateTimeOffset.UtcNow.AddDays(30),
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Issue_WithAdminRole_Returns201_AndThenGetReturnsTheCoupon()
    {
        var couponCode = $"ADMIN-{Guid.NewGuid():N}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/coupons")
        {
            Content = JsonContent.Create(new
            {
                couponCode,
                perUserCap = 1,
                globalCap = 100,
                validFrom = DateTimeOffset.UtcNow,
                validUntil = DateTimeOffset.UtcNow.AddDays(30),
            }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        created.GetProperty("couponCode").GetString().Should().Be(couponCode.ToUpperInvariant());

        var getRequest = new HttpRequestMessage(HttpMethod.Get, $"/v1/coupons/{couponCode.ToUpperInvariant()}");
        getRequest.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");
        var getResponse = await _client.SendAsync(getRequest);

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Deactivate_WithStaleIfMatch_Returns412()
    {
        var couponCode = $"STALE-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        _factory.Store.Coupons[couponCode] = Coupon.Issue(couponCode, null, null, now, now.AddDays(30), "admin", now).Value;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/coupons/{couponCode}/deactivate");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", "999");
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be((HttpStatusCode)412);
    }

    [Fact]
    public async Task Deactivate_WithCorrectIfMatch_Returns200()
    {
        var couponCode = $"OK-{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;
        var coupon = Coupon.Issue(couponCode, null, null, now, now.AddDays(30), "admin", now).Value;
        _factory.Store.Coupons[couponCode] = coupon;

        var request = new HttpRequestMessage(HttpMethod.Post, $"/v1/coupons/{couponCode}/deactivate");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        request.Headers.TryAddWithoutValidation("If-Match", coupon.Version.ToString());
        request.Headers.Add(TestAuthenticationHandler.RolesHeader, "admin");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MetricsEndpoint_IsExposed_ForPrometheusScraping()
    {
        var response = await _client.GetAsync("/metrics");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
