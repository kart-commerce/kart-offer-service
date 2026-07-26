using KartOfferService.Domain.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartOfferService.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="Coupon"/> to database-design.md's `coupons` table exactly.</summary>
public sealed class CouponConfiguration : IEntityTypeConfiguration<Coupon>
{
    public void Configure(EntityTypeBuilder<Coupon> builder)
    {
        builder.ToTable("coupons");

        builder.HasKey(c => c.CouponCode);
        builder.Property(c => c.CouponCode).HasColumnName("coupon_code").HasColumnType("text").ValueGeneratedNever();

        builder.Property(c => c.PerUserCap).HasColumnName("per_user_cap");
        builder.Property(c => c.GlobalCap).HasColumnName("global_cap");
        builder.Property(c => c.ValidFrom).HasColumnName("valid_from").IsRequired();
        builder.Property(c => c.ValidUntil).HasColumnName("valid_until").IsRequired();
        builder.Property(c => c.TotalRedemptions).HasColumnName("total_redemptions").IsRequired();

        // database-design.md's optimistic-concurrency token (api-contract.yaml `CouponAdminView.version`) -
        // EF appends `WHERE version = @original_version` to the UPDATE automatically, so a
        // concurrent write between DeactivateCouponCommandHandler's own version check and its
        // SaveChangesAsync still throws DbUpdateConcurrencyException rather than silently
        // clobbering a racing write.
        builder.Property(c => c.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        builder.HasIndex(c => new { c.ValidFrom, c.ValidUntil }).HasDatabaseName("idx_coupons_validity_window");

        builder.Ignore(c => c.DomainEvents);
    }
}
