using FluentAssertions;
using KartOfferService.Domain.Coupons;
using KartOfferService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartOfferService.IntegrationTests;

/// <summary>
/// Verifies OfferDbContext.SaveChangesAsync's domain-event-to-outbox conversion against real
/// PostgreSQL - the outbox row must land in the same transaction as the aggregate write it
/// describes. This is the mechanism the entire read-model sync pipeline (Postgres write ->
/// outbox -> RabbitMQ -> Mongo read model) depends on being correct.
/// </summary>
public sealed class OfferOutboxTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("kart_offer_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private OfferDbContext _dbContext = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        var options = new DbContextOptionsBuilder<OfferDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;

        _dbContext = new OfferDbContext(options);
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact]
    public async Task SaveChangesAsync_OnCouponIssue_WritesOneOutboxRow_InTheSameTransaction()
    {
        var now = DateTimeOffset.UtcNow;
        var coupon = Coupon.Issue("SAVE10", null, null, now, now.AddDays(30), "admin", now).Value;

        _dbContext.Coupons.Add(coupon);
        await _dbContext.SaveChangesAsync();

        coupon.DomainEvents.Should().BeEmpty("SaveChangesAsync must clear raised events once persisted");

        var outboxRows = await _dbContext.OutboxEvents.Where(e => e.EventType == "CouponIssued").ToListAsync();
        outboxRows.Should().ContainSingle();
        outboxRows[0].Payload.Should().Contain("SAVE10");
        outboxRows[0].PublishedAt.Should().BeNull();
        outboxRows[0].CreatedBy.Should().Be("admin");
    }

    [Fact]
    public async Task SaveChangesAsync_OnCouponRedeem_WritesCouponRedeemedOutboxRow()
    {
        var now = DateTimeOffset.UtcNow;
        var coupon = Coupon.Issue("SAVE20", null, null, now.AddDays(-1), now.AddDays(30), "admin", now.AddDays(-1)).Value;
        _dbContext.Coupons.Add(coupon);
        await _dbContext.SaveChangesAsync();

        var redemption = coupon.Redeem("user-1", "order-1", 0, "user-1", now).Value;
        _dbContext.CouponRedemptions.Add(redemption);
        await _dbContext.SaveChangesAsync();

        var outboxRows = await _dbContext.OutboxEvents.Where(e => e.EventType == "CouponRedeemed").ToListAsync();
        outboxRows.Should().ContainSingle();
        outboxRows[0].Payload.Should().Contain("order-1");
    }

    [Fact]
    public async Task Coupons_EnforcesUniquePrimaryKey_OnCouponCode()
    {
        var now = DateTimeOffset.UtcNow;
        _dbContext.Coupons.Add(Coupon.Issue("DUP", null, null, now, now.AddDays(1), "admin", now).Value);
        await _dbContext.SaveChangesAsync();

        // A second DbContext, not a second Add on the same tracked context - EF's own change
        // tracker would reject two tracked instances sharing a key before ever reaching the
        // database, which would prove nothing about the actual PostgreSQL constraint.
        var options = new DbContextOptionsBuilder<OfferDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var otherContext = new OfferDbContext(options);
        otherContext.Coupons.Add(Coupon.Issue("DUP", null, null, now, now.AddDays(1), "admin", now).Value);
        var act = async () => await otherContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CouponRedemptions_EnforcesUniqueCouponCodeOrderIdPair()
    {
        // edge-cases.md #1: the real double-redemption guard is this DB constraint, not domain
        // logic - Coupon.Redeem itself has no "already redeemed this order" check, since that's a
        // cross-cutting concern the unique index enforces atomically regardless of caller.
        var now = DateTimeOffset.UtcNow;
        var coupon = Coupon.Issue("SAVE30", null, null, now.AddDays(-1), now.AddDays(30), "admin", now.AddDays(-1)).Value;
        _dbContext.Coupons.Add(coupon);
        var firstRedemption = coupon.Redeem("user-1", "order-1", 0, "user-1", now).Value;
        _dbContext.CouponRedemptions.Add(firstRedemption);
        await _dbContext.SaveChangesAsync();

        var secondRedemption = coupon.Redeem("user-1", "order-1", 0, "user-1", now.AddMinutes(1)).Value;
        _dbContext.CouponRedemptions.Add(secondRedemption);
        var act = async () => await _dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
