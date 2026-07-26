using System.Text.Json;
using System.Text.Json.Serialization;
using KartOfferService.Domain.Promotions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartOfferService.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="PromotionCampaign"/> to database-design.md's `promotion_campaigns` table - `discount_rule` is the only JSONB column in this schema, decomposing ddd-model.md's Strategy hierarchy to/from a small type-discriminated JSON shape.</summary>
public sealed class PromotionCampaignConfiguration : IEntityTypeConfiguration<PromotionCampaign>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<PromotionCampaign> builder)
    {
        builder.ToTable("promotion_campaigns");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("campaign_id").ValueGeneratedNever();

        builder.OwnsOne(c => c.Window, window =>
        {
            window.Property(w => w.StartsAt).HasColumnName("starts_at").IsRequired();
            window.Property(w => w.EndsAt).HasColumnName("ends_at").IsRequired();
            window.HasIndex(w => new { w.StartsAt, w.EndsAt }).HasDatabaseName("idx_promotion_campaigns_window");
        });
        builder.Navigation(c => c.Window).IsRequired();

        builder.Property(c => c.DiscountRule)
            .HasColumnName("discount_rule")
            .HasColumnType("jsonb")
            .HasConversion(
                rule => JsonSerializer.Serialize(ToJsonShape(rule), SerializerOptions),
                json => FromJsonShape(JsonSerializer.Deserialize<DiscountRuleJson>(json, SerializerOptions)!))
            .IsRequired();

        // database-design.md's optimistic-concurrency token, same mechanics as CouponConfiguration.
        builder.Property(c => c.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();

        builder.Property(c => c.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(c => c.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(c => c.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        builder.Ignore(c => c.DomainEvents);
    }

    private static DiscountRuleJson ToJsonShape(DiscountRule rule) => rule switch
    {
        PercentageOffDiscountRule p => new DiscountRuleJson("percentageOff", p.PercentageOff),
        FixedAmountOffDiscountRule f => new DiscountRuleJson("fixedAmountOff", f.AmountOff),
        _ => throw new InvalidOperationException($"No JSON mapping for discount rule type '{rule.GetType().Name}'."),
    };

    private static DiscountRule FromJsonShape(DiscountRuleJson json) => json.Type switch
    {
        "percentageOff" => new PercentageOffDiscountRule(json.Value),
        "fixedAmountOff" => new FixedAmountOffDiscountRule(json.Value),
        _ => throw new InvalidOperationException($"No domain mapping for discount_rule.type '{json.Type}'."),
    };

    private sealed record DiscountRuleJson([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("value")] decimal Value);
}
