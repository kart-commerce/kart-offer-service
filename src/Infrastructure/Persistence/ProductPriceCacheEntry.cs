namespace KartOfferService.Infrastructure.Persistence;

/// <summary>
/// architecture.md's locally-materialized catalog price, backing `IProductPriceCache`
/// (KartOfferService.Application.Common.Interfaces). Not a domain aggregate (no invariants beyond
/// "latest write wins") - a plain write-side cache row, not a `PromotionCampaign`/`Coupon`/
/// `PricingQuote` concept per ddd-model.md.
/// </summary>
public sealed class ProductPriceCacheEntry
{
    public string Sku { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; }

    private ProductPriceCacheEntry()
    {
    }

    public ProductPriceCacheEntry(string sku, decimal price, string currency, DateTimeOffset updatedAt)
    {
        Sku = sku;
        Price = price;
        Currency = currency;
        UpdatedAt = updatedAt;
    }

    public void Update(decimal price, string currency, DateTimeOffset updatedAt)
    {
        Price = price;
        Currency = currency;
        UpdatedAt = updatedAt;
    }
}
