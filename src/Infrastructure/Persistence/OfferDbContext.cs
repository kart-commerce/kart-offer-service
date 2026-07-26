using KartOfferService.Domain.Common;
using KartOfferService.Domain.Coupons;
using KartOfferService.Domain.Pricing;
using KartOfferService.Domain.Promotions;
using KartOfferService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace KartOfferService.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL write side (database-design.md) - the source of truth for all three aggregates.
/// There is deliberately no MongoDB anywhere in this DbContext; the read side is a separate,
/// eventually-consistent projection kept in sync via the outbox (see
/// <see cref="SaveChangesAsync(CancellationToken)"/> and Infrastructure/Messaging).
/// </summary>
public sealed class OfferDbContext : DbContext
{
    public OfferDbContext(DbContextOptions<OfferDbContext> options) : base(options)
    {
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

        foreach (var entity in entitiesWithEvents)
        {
            var aggregateId = ResolveAggregateId(entity);
            var actingPrincipal = ResolveActingPrincipal(entity);

            foreach (var domainEvent in entity.DomainEvents)
            {
                OutboxEvents.Add(OfferOutboxEvent.FromDomainEvent(domainEvent, aggregateId, actingPrincipal));
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
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
