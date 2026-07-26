using KartOfferService.Domain.Pricing;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>
/// architecture.md's key constraint: `/pricing/quote` must be self-contained, never a synchronous
/// fan-out to Product - catalog price is materialized locally from the consumed `ProductPriceChanged`
/// event (OFF-4 writes here; OFF-8 reads here). Backed by a small PostgreSQL table, not Mongo -
/// this is write-side state a money computation depends on, not a read-optimized query projection.
/// </summary>
public interface IProductPriceCache
{
    /// <summary>Commits immediately - this cache carries no domain events/outbox rows, so it does not participate in <see cref="IUnitOfWork"/>.</summary>
    Task UpsertAsync(string sku, Money price, DateTimeOffset now, CancellationToken cancellationToken);

    Task<Money?> GetAsync(string sku, CancellationToken cancellationToken);
}
