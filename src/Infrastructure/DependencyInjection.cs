using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Infrastructure.Messaging;
using KartOfferService.Infrastructure.Persistence;
using KartOfferService.Infrastructure.Persistence.ReadModel;
using KartOfferService.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace KartOfferService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        AddWriteSidePersistence(services, configuration);
        AddReadSidePersistence(services, configuration);
        AddMessaging(services, configuration);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentPrincipal, HttpCurrentPrincipal>();

        return services;
    }

    /// <summary>PostgreSQL - the sole write-side source of truth for all three aggregates (database-design.md).</summary>
    private static void AddWriteSidePersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OfferDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("OfferDatabase")));

        services.AddScoped<ICouponRepository, CouponRepository>();
        services.AddScoped<ICouponRedemptionRepository, CouponRedemptionRepository>();
        services.AddScoped<IPricingQuoteRepository, PricingQuoteRepository>();
        services.AddScoped<IPromotionCampaignRepository, PromotionCampaignRepository>();
        services.AddScoped<IProductPriceCache, ProductPriceCache>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
    }

    /// <summary>
    /// MongoDB (sharded in production) - the CQRS read side. Denormalized, eventually-consistent
    /// projections kept in sync from PostgreSQL exclusively via <see cref="ReadModelProjectionConsumerHostedService"/>;
    /// never written to by a request handler.
    /// </summary>
    private static void AddReadSidePersistence(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MongoOptions>(configuration.GetSection("Mongo"));

        services.AddSingleton<IMongoClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            var settings = MongoClientSettings.FromConnectionString(options.ConnectionString);
            // requirement-spec.md's P95<150ms/P99<400ms SLA: a checkout-path read should fail fast
            // into the global exception handler during a shard/replica-set outage, not hang for
            // the driver's 30s default server-selection timeout.
            settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            return new MongoClient(settings);
        });
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
            return new OfferReadDbContext(sp.GetRequiredService<IMongoClient>().GetDatabase(options.Database));
        });
        services.AddHostedService<MongoIndexInitializerHostedService>();

        services.AddScoped<ICouponReadRepository, CouponReadRepository>();
        services.AddScoped<IPromotionReadRepository, PromotionReadRepository>();
        services.AddScoped<ReadModelProjectionWriter>();
    }

    /// <summary>
    /// contracts/message-bus-manifest.json is the single source of truth for this service's
    /// entire RabbitMQ topology - every exchange, queue, binding, dead-letter and retry-tier name.
    /// Nothing messaging-related is hardcoded in C#: the manifest is loaded once here and shared
    /// as a singleton; RabbitMqTopologyProvisioner scans it to declare the topology.
    /// </summary>
    private static void AddMessaging(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
            var manifestPath = Path.IsPathRooted(options.ManifestPath)
                ? options.ManifestPath
                : Path.Combine(AppContext.BaseDirectory, options.ManifestPath);
            return MessageBusManifestLoader.Load(manifestPath);
        });
        services.AddSingleton<IConnectionFactory>(sp => new ConnectionFactory
        {
            HostName = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value.HostName,
            DispatchConsumersAsync = true,
        });

        services.AddHostedService<RabbitMqTopologyStartupHostedService>();
        services.AddHostedService<OutboxRelayHostedService>();
        services.AddHostedService<ProductEventsConsumerHostedService>();
        services.AddHostedService<OrderEventsConsumerHostedService>();
        services.AddHostedService<ReadModelProjectionConsumerHostedService>();
    }
}
