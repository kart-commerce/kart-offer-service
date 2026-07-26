using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartOfferService.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="ProductPriceCacheEntry"/> to `product_price_cache` - not part of the approved database-design.md schema, added to satisfy architecture.md's "materialized locally" constraint (see ProductPriceCacheEntry's own remarks).</summary>
public sealed class ProductPriceCacheEntryConfiguration : IEntityTypeConfiguration<ProductPriceCacheEntry>
{
    public void Configure(EntityTypeBuilder<ProductPriceCacheEntry> builder)
    {
        builder.ToTable("product_price_cache");

        builder.HasKey(p => p.Sku);
        builder.Property(p => p.Sku).HasColumnName("sku").HasColumnType("text").ValueGeneratedNever();

        builder.Property(p => p.Price).HasColumnName("price").HasColumnType("numeric(12,2)").IsRequired();
        builder.Property(p => p.Currency).HasColumnName("currency").HasColumnType("text").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
