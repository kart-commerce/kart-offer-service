using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KartOfferService.Infrastructure.Persistence.Configurations;

/// <summary>Maps <see cref="OfferOutboxEvent"/> to `offer_outbox_events` - the Transactional Outbox pattern (database-design.md).</summary>
public sealed class OfferOutboxEventConfiguration : IEntityTypeConfiguration<OfferOutboxEvent>
{
    public void Configure(EntityTypeBuilder<OfferOutboxEvent> builder)
    {
        builder.ToTable("offer_outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();

        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(e => e.EventType).HasColumnName("event_type").HasColumnType("text").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at").IsRequired();
        builder.Property(e => e.PublishedAt).HasColumnName("published_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasColumnType("text").IsRequired();
        builder.Property(e => e.UpdatedBy).HasColumnName("updated_by").HasColumnType("text").IsRequired();

        // OutboxRelayHostedService's "pending rows, oldest first" poll.
        builder.HasIndex(e => e.OccurredAt)
            .HasDatabaseName("idx_offer_outbox_unpublished")
            .HasFilter("published_at IS NULL");
    }
}
