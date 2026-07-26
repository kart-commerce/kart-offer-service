using FluentAssertions;
using KartOfferService.Infrastructure.Persistence.ReadModel;
using KartOfferService.IntegrationTests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace KartOfferService.IntegrationTests;

[Collection("Mongo")]
public class MongoIndexInitializerHostedServiceTests
{
    private readonly OfferReadDbContext _context;

    public MongoIndexInitializerHostedServiceTests(MongoContainerFixture fixture)
    {
        _context = new OfferReadDbContext(fixture.Database);
    }

    [Fact]
    public async Task DeclareIndexesAsync_DeclaresTheActiveWindowIndex_AgainstARealServer()
    {
        var service = new MongoIndexInitializerHostedService(_context, NullLogger<MongoIndexInitializerHostedService>.Instance);

        await service.DeclareIndexesAsync(CancellationToken.None);

        var indexes = await (await _context.PromotionCampaigns.Indexes.ListAsync()).ToListAsync();
        indexes.Should().Contain(i => i["name"] == "startsAt_1_endsAt_1");
    }
}
