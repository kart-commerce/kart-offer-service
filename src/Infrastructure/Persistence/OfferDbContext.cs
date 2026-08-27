using KartOfferService.Domain.Common;
using KartOfferService.Domain.Coupons;
using KartOfferService.Domain.Pricing;
using KartOfferService.Domain.Promotions;
using KartOfferService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KartOfferService.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL write side (database-design.md) - the source of truth for all three aggregates.
/// There is deliberately no MongoDB anywhere in this DbContext; the read side is a separate,
/// eventually-consistent projection kept in sync via the outbox (see
/// <see cref="SaveChangesAsync(CancellationToken)"/> and Infrastructure/Messaging).
/// </summary>
public sealed class OfferDbContext : DbContext
{
    private readonly ILogger<OfferDbContext> _logger;

    // `logger` defaults to a no-op instance so OfferDbContextFactory's design-time
    // (`dotnet ef migrations ...`) construction path, and any test that builds this DbContext
    // directly with only DbContextOptions, keep compiling unchanged - the runtime DI container
    // always supplies a real one via the generic ILogger<T> registration every service gets for free.
    public OfferDbContext(DbContextOptions<OfferDbContext> options, ILogger<OfferDbContext>? logger = null) : base(options)
    {
        _logger = logger ?? NullLogger<OfferDbContext>.Instance;
    }

    public DbSet<Coupon> Coupons => Set<Coupon>();

    public DbSet<CouponRedemption> CouponRedemptions => Set<CouponRedemption>();

    public DbSet<PricingQuote> PricingQuotes => Set<PricingQuote>();

    public DbSet<PromotionCampaign> PromotionCampaigns => Set<PromotionCampaign>();

    public DbSet<ProductPriceCacheEntry> ProductPriceCache => Set<ProductPriceCacheEntry>();

    public DbSet<OfferOutboxEvent> OutboxEvents => Set<OfferOutboxEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CouponConfiguration());
        modelBuilder.ApplyConfiguration(new CouponRedemptionConfiguration());
        modelBuilder.ApplyConfiguration(new PricingQuoteConfiguration());
        modelBuilder.ApplyConfiguration(new PromotionCampaignConfiguration());
        modelBuilder.ApplyConfiguration(new ProductPriceCacheEntryConfiguration());
        modelBuilder.ApplyConfiguration(new OfferOutboxEventConfiguration());
    }

    /// <summary>
    /// Converts every tracked aggregate's pending domain events into `offer_outbox_events` rows
    /// within this same call (design-decisions.md "Global Exception Handling"'s sibling concern,
    /// Event Publication Reliability) - the write and "the event will eventually publish" commit
    /// atomically, never as a separate, unguarded publish step.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = ChangeTracker.Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)
            .Where(entity => entity.DomainEvents.Count > 0)
            .ToList();

        var enqueuedByAggregate = new List<(string EntityTypeName, Guid AggregateId, List<OfferOutboxEvent> OutboxEvents)>();

        foreach (var entity in entitiesWithEvents)
        {
            var aggregateId = ResolveAggregateId(entity);
            var actingPrincipal = ResolveActingPrincipal(entity);
            var enqueued = new List<OfferOutboxEvent>();

            foreach (var domainEvent in entity.DomainEvents)
            {
                var outboxEvent = OfferOutboxEvent.FromDomainEvent(domainEvent, aggregateId, actingPrincipal);
                OutboxEvents.Add(outboxEvent);
                enqueued.Add(outboxEvent);
            }

            enqueuedByAggregate.Add((entity.GetType().Name, aggregateId, enqueued));
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        foreach (var (entityTypeName, aggregateId, outboxEvents) in enqueuedByAggregate)
        {
            if (outboxEvents.Count == 0)
            {
                continue;
            }

            _logger.LogInformation(
                "Stage {Stage}: {EntityType} {AggregateId} persisted, outbox event(s) {OutboxEventIds} ({EventTypes}) enqueued",
                $"{entityTypeName}PersistedOutboxEventEnqueued",
                entityTypeName,
                aggregateId,
                string.Join(",", outboxEvents.Select(e => e.Id)),
                string.Join(",", outboxEvents.Select(e => e.EventType)));
        }

        return result;
    }

    private static Guid ResolveAggregateId(IHasDomainEvents entity) => entity switch
    {
        Coupon coupon => AggregateIdHasher.ForCouponCode(coupon.CouponCode),
        PricingQuote quote => quote.Id,
        PromotionCampaign campaign => campaign.Id,
        _ => Guid.Empty,
    };

    private static string ResolveActingPrincipal(IHasDomainEvents entity) => entity switch
    {
        Coupon coupon => coupon.UpdatedBy,
        PricingQuote quote => quote.UpdatedBy,
        PromotionCampaign campaign => campaign.UpdatedBy,
        _ => "system:unknown",
    };
}
