using FluentAssertions;
using KartOfferService.Domain.Promotions;
using KartOfferService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace KartOfferService.IntegrationTests;

/// <summary>
/// Exercises PromotionCampaignRepository against real PostgreSQL - validates the owned-type
/// (`Window`) mapping and the `GetActiveAsync` window-comparison query, which GetPricingQuote
/// (OFF-8) depends on for its self-contained, in-process discount computation.
/// </summary>
public sealed class PromotionCampaignRepositoryTests : IAsyncLifetime
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
    public async Task GetActiveAsync_ReturnsOnlyCampaignsWithinWindow()
    {
        var now = DateTimeOffset.UtcNow;
        var repository = new PromotionCampaignRepository(_dbContext);

        var active = PromotionCampaign.Create(now.AddDays(-1), now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", now).Value;
        var notYetStarted = PromotionCampaign.Create(now.AddDays(1), now.AddDays(7), PercentageOffDiscountRule.Create(10).Value, "admin", now).Value;
        var alreadyEnded = PromotionCampaign.Create(now.AddDays(-10), now.AddDays(-1), PercentageOffDiscountRule.Create(10).Value, "admin", now).Value;

        _dbContext.PromotionCampaigns.AddRange(active, notYetStarted, alreadyEnded);
        await _dbContext.SaveChangesAsync();

        var results = await repository.GetActiveAsync(now, CancellationToken.None);

        results.Should().ContainSingle(c => c.Id == active.Id);
        results.Should().NotContain(c => c.Id == notYetStarted.Id || c.Id == alreadyEnded.Id);
    }

    [Fact]
    public async Task RoundTrips_FixedAmountOffDiscountRule_ThroughJsonbColumn()
    {
        var now = DateTimeOffset.UtcNow;
        var campaign = PromotionCampaign.Create(now, now.AddDays(7), FixedAmountOffDiscountRule.Create(15).Value, "admin", now).Value;
        _dbContext.PromotionCampaigns.Add(campaign);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _dbContext.PromotionCampaigns.FirstAsync(c => c.Id == campaign.Id);

        reloaded.DiscountRule.Should().BeOfType<FixedAmountOffDiscountRule>();
        ((FixedAmountOffDiscountRule)reloaded.DiscountRule).AmountOff.Should().Be(15);
    }
}
