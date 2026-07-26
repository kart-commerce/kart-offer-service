using KartOfferService.Domain.Pricing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartOfferService.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PricingQuote"/> to database-design.md's append-only `pricing_quotes` table.</summary>
public sealed class PricingQuoteConfiguration : IEntityTypeConfiguration<PricingQuote>
{
    public void Configure(EntityTypeBuilder<PricingQuote> builder)
    {
        builder.ToTable("pricing_quotes");

        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("quote_id").ValueGeneratedNever();

        // Money is a value object (record), not its own entity/table - owned-type mapping
        // decomposes it into this same row's `currency`/`total_amount` columns.
        builder.OwnsOne(q => q.Total, money =>
        {
            money.Property(m => m.Amount).HasColumnName("total_amount").HasColumnType("numeric(12,2)").IsRequired();
            money.Property(m => m.Currency).HasColumnName("currency").HasColumnType("text").IsRequired();
        });
        builder.Navigation(q => q.Total).IsRequired();

        builder.Property(q => q.IssuedAt).HasColumnName("issued_at").IsRequired();
        builder.Property(q => q.ExpiresAt).HasColumnName("expires_at").IsRequired();
        builder.Property(q => q.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(q => q.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(q => q.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        builder.Ignore(q => q.DomainEvents);
    }
}
