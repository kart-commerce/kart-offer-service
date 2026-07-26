using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Infrastructure.Messaging;
using KartOfferService.Infrastructure.Persistence.ReadModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace KartOfferService.ContractTests;

/// <summary>
/// Boots the real Api + Application pipeline but swaps PostgreSQL/MongoDB/RabbitMQ for in-memory
/// fakes and real Identity-issued JWT validation for a header-driven test scheme - these tests
/// check the HTTP wire contract (status codes, JSON field names, RBAC gating) against
/// api-contract.yaml, not persistence/messaging mechanics (already covered by IntegrationTests
/// against real engines).
/// </summary>
public sealed class OfferContractTestFactory : WebApplicationFactory<Program>
{
    public InMemoryOfferStore Store { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Store);

            services.RemoveAll<ICouponRepository>();
            services.AddSingleton<ICouponRepository, InMemoryCouponRepository>();
            services.RemoveAll<ICouponRedemptionRepository>();
            services.AddSingleton<ICouponRedemptionRepository, InMemoryCouponRedemptionRepository>();
            services.RemoveAll<IPricingQuoteRepository>();
            services.AddSingleton<IPricingQuoteRepository, InMemoryPricingQuoteRepository>();
            services.RemoveAll<IPromotionCampaignRepository>();
            services.AddSingleton<IPromotionCampaignRepository, InMemoryPromotionCampaignRepository>();
            services.RemoveAll<IProductPriceCache>();
            services.AddSingleton<IProductPriceCache, InMemoryProductPriceCache>();
            services.RemoveAll<ICouponReadRepository>();
            services.AddSingleton<ICouponReadRepository, InMemoryCouponReadRepository>();
            services.RemoveAll<IPromotionReadRepository>();
            services.AddSingleton<IPromotionReadRepository, InMemoryPromotionReadRepository>();
            services.RemoveAll<IUnitOfWork>();
            services.AddSingleton<IUnitOfWork, NoOpUnitOfWork>();
            services.RemoveAll<ICurrentPrincipal>();
            services.AddSingleton<ICurrentPrincipal, TestCurrentPrincipal>();

            // No real PostgreSQL/MongoDB/RabbitMQ in the contract-test environment - these tests
            // assert HTTP behavior, not messaging/read-model-sync behavior (already covered
            // separately in IntegrationTests).
            RemoveHostedService<RabbitMqTopologyStartupHostedService>(services);
            RemoveHostedService<OutboxRelayHostedService>(services);
            RemoveHostedService<ProductEventsConsumerHostedService>(services);
            RemoveHostedService<OrderEventsConsumerHostedService>(services);
            RemoveHostedService<ReadModelProjectionConsumerHostedService>(services);
            RemoveHostedService<MongoIndexInitializerHostedService>(services);

            services.AddAuthentication(TestAuthenticationHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(TestAuthenticationHandler.SchemeName, _ => { });
            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthenticationHandler.SchemeName;
                options.DefaultScheme = TestAuthenticationHandler.SchemeName;
            });
        });
    }

    private static void RemoveHostedService<T>(IServiceCollection services)
        where T : class, IHostedService
    {
        var descriptor = services.FirstOrDefault(d => d.ImplementationType == typeof(T));
        if (descriptor is not null)
        {
            services.Remove(descriptor);
        }
    }
}
