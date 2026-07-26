using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Pricing;
using Microsoft.EntityFrameworkCore;

namespace KartOfferService.Infrastructure.Persistence;

public sealed class ProductPriceCache : IProductPriceCache
{
    private readonly OfferDbContext _dbContext;

    public ProductPriceCache(OfferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpsertAsync(string sku, Money price, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.ProductPriceCache.FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
        if (existing is null)
        {
            await _dbContext.ProductPriceCache.AddAsync(new ProductPriceCacheEntry(sku, price.Amount, price.Currency, now), cancellationToken);
        }
        else
        {
            existing.Update(price.Amount, price.Currency, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<Money?> GetAsync(string sku, CancellationToken cancellationToken)
    {
        var entry = await _dbContext.ProductPriceCache.AsNoTracking().FirstOrDefaultAsync(p => p.Sku == sku, cancellationToken);
        return entry is null ? null : new Money(entry.Price, entry.Currency);
    }
}
