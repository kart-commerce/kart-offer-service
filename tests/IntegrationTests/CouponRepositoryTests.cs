using FluentAssertions;
using KartOfferService.Domain.Coupons;
using KartOfferService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartOfferService.IntegrationTests;

/// <summary>
/// Exercises CouponRepository against a real PostgreSQL engine - validates the EF mapping, the
/// `SELECT ... FOR UPDATE` raw-SQL path, and the `version` optimistic-concurrency token
/// (database-design.md's "Admin Write-Path Mechanics"), none of which an in-memory provider can
/// faithfully verify.
/// </summary>
public sealed class CouponRepositoryTests : IAsyncLifetime
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
    public async Task GetForUpdateAsync_ReturnsTheSameCouponAsGetAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var coupon = Coupon.Issue("FOR-UPDATE", 1, 100, now, now.AddDays(30), "admin", now).Value;
        _dbContext.Coupons.Add(coupon);
        await _dbContext.SaveChangesAsync();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync();
        var repository = new CouponRepository(_dbContext);
        var locked = await repository.GetForUpdateAsync("FOR-UPDATE", CancellationToken.None);

        locked.Should().NotBeNull();
        locked!.CouponCode.Should().Be("FOR-UPDATE");
        locked.PerUserCap.Should().Be(1);
        await transaction.CommitAsync();
    }

    [Fact]
    public async Task ConcurrentUpdate_WithStaleVersion_ThrowsConcurrencyException()
    {
        var now = DateTimeOffset.UtcNow;
        var coupon = Coupon.Issue("VERSIONED", null, null, now, now.AddDays(30), "admin", now).Value;
        _dbContext.Coupons.Add(coupon);
        await _dbContext.SaveChangesAsync();

        // A second DbContext simulates a concurrent request that read the same row.
        var options = new DbContextOptionsBuilder<OfferDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options;
        await using var otherContext = new OfferDbContext(options);
        var staleCopy = await otherContext.Coupons.FirstAsync(c => c.CouponCode == "VERSIONED");

        // First writer deactivates and commits, bumping version 1 -> 2.
        var freshCopy = await _dbContext.Coupons.FirstAsync(c => c.CouponCode == "VERSIONED");
        freshCopy.Deactivate("admin", now.AddDays(1));
        await _dbContext.SaveChangesAsync();

        // Second writer still holds version 1 - EF's concurrency token must reject this write.
        staleCopy.Deactivate("admin", now.AddDays(2));
        var act = async () => await otherContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
