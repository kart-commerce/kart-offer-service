using KartOfferService.Domain.Coupons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartOfferService.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="CouponRedemption"/> to database-design.md's `coupon_redemptions` table exactly - the real double-redemption guard is the `(coupon_code, order_id)` unique index.</summary>
public sealed class CouponRedemptionConfiguration : IEntityTypeConfiguration<CouponRedemption>
{
    public void Configure(EntityTypeBuilder<CouponRedemption> builder)
    {
        builder.ToTable("coupon_redemptions");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(r => r.CouponCode).HasColumnName("coupon_code").HasColumnType("text").IsRequired();
        builder.Property(r => r.UserId).HasColumnName("user_id").HasColumnType("text").IsRequired();
        builder.Property(r => r.OrderId).HasColumnName("order_id").HasColumnType("text").IsRequired();
        builder.Property(r => r.RedeemedAt).HasColumnName("redeemed_at").IsRequired();
        builder.Property(r => r.VoidedAt).HasColumnName("voided_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(r => r.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(r => r.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        builder.HasOne<Coupon>()
            .WithMany()
            .HasForeignKey(r => r.CouponCode)
            .HasConstraintName("FK_coupon_redemptions_coupon_code")
            .OnDelete(DeleteBehavior.Restrict);

        // edge-cases.md #1 - the real double-redemption guard, defense-in-depth behind the
        // per-coupon `SELECT ... FOR UPDATE` lock RedeemCouponCommandHandler already takes.
        builder.HasIndex(r => new { r.CouponCode, r.OrderId })
            .HasDatabaseName("idx_coupon_redemptions_coupon_order")
            .IsUnique();

        builder.HasIndex(r => new { r.CouponCode, r.UserId })
            .HasDatabaseName("idx_coupon_redemptions_user")
            .HasFilter("voided_at IS NULL");

        builder.HasIndex(r => r.OrderId).HasDatabaseName("idx_coupon_redemptions_order_id");
    }
}
