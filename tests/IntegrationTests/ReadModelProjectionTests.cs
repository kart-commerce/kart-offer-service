using FluentAssertions;
using KartOfferService.Infrastructure.Persistence.ReadModel;
using KartOfferService.IntegrationTests.Fixtures;
using Xunit;

namespace KartOfferService.IntegrationTests;

/// <summary>
/// Verifies the CQRS sync path end-to-end against a real MongoDB server: `ReadModelProjectionWriter`
/// (the consumer's write path) followed by `CouponReadRepository`/`PromotionReadRepository` (the
/// query handlers' read path) - proving a projected write is actually visible to the read side the
/// user's requirement calls for.
/// </summary>
[Collection("Mongo")]
public class ReadModelProjectionTests
{
    private readonly OfferReadDbContext _context;
    private readonly ReadModelProjectionWriter _writer;

    public ReadModelProjectionTests(MongoContainerFixture fixture)
    {
        _context = new OfferReadDbContext(fixture.Database);
        _writer = new ReadModelProjectionWriter(_context);
    }

    [Fact]
    public async Task UpsertCouponAsync_ThenIncrementCouponRedemptionsAsync_IsVisibleViaReadRepository()
    {
        var couponCode = $"PROJ-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        var readRepository = new CouponReadRepository(_context);

        await _writer.UpsertCouponAsync(couponCode, perUserCap: 1, globalCap: 100, now.AddDays(-1), now.AddDays(30), now, CancellationToken.None);
        await _writer.IncrementCouponRedemptionsAsync(couponCode, +1, now, CancellationToken.None);
        await _writer.IncrementCouponRedemptionsAsync(couponCode, +1, now, CancellationToken.None);

        var projected = await readRepository.GetAsync(couponCode, CancellationToken.None);

        projected.Should().NotBeNull();
        projected!.TotalRedemptions.Should().Be(2);
        projected.GlobalCap.Should().Be(100);
    }

    [Fact]
    public async Task UpsertCouponAsync_ThenDeactivate_TruncatesValidUntilInReadModel()
    {
        var couponCode = $"PROJ-{Guid.NewGuid():N}";
        var now = DateTime.UtcNow;
        var readRepository = new CouponReadRepository(_context);

        await _writer.UpsertCouponAsync(couponCode, null, null, now.AddDays(-1), now.AddDays(30), now, CancellationToken.None);
        await _writer.UpdateCouponValidUntilAsync(couponCode, now.AddDays(1), now, CancellationToken.None);

        var projected = await readRepository.GetAsync(couponCode, CancellationToken.None);

        projected!.ValidUntil.Should().BeCloseTo(new DateTimeOffset(now.AddDays(1), TimeSpan.Zero), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task UpsertPromotionAsync_IsVisibleViaGetActiveAsync_AndInvisibleOnceDeactivatedOutsideWindow()
    {
        var campaignId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var readRepository = new PromotionReadRepository(_context);

        await _writer.UpsertPromotionAsync(campaignId.ToString(), now.AddDays(-1), now.AddDays(7), now, CancellationToken.None);
        var activeBeforeDeactivation = await readRepository.GetActiveAsync(new DateTimeOffset(now, TimeSpan.Zero), CancellationToken.None);
        activeBeforeDeactivation.Should().Contain(p => p.CampaignId == campaignId);

        await _writer.UpdatePromotionEndsAtAsync(campaignId.ToString(), now.AddMinutes(-1), now, CancellationToken.None);
        var activeAfterDeactivation = await readRepository.GetActiveAsync(new DateTimeOffset(now, TimeSpan.Zero), CancellationToken.None);

        activeAfterDeactivation.Should().NotContain(p => p.CampaignId == campaignId);
    }
}
