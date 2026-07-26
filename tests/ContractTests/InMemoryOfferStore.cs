using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Domain.Coupons;
using KartOfferService.Domain.Pricing;
using KartOfferService.Domain.Promotions;

namespace KartOfferService.ContractTests;

/// <summary>
/// Backs every in-memory fake repository these contract tests wire in place of real
/// PostgreSQL/MongoDB - these tests assert HTTP wire shape (status codes, JSON field names, RBAC
/// gating) against api-contract.yaml, not persistence/locking mechanics (already covered by
/// IntegrationTests against real engines).
/// </summary>
public sealed class InMemoryOfferStore
{
    public Dictionary<string, Coupon> Coupons { get; } = new();
    public List<CouponRedemption> Redemptions { get; } = new();
    public List<PricingQuote> Quotes { get; } = new();
    public Dictionary<Guid, PromotionCampaign> Campaigns { get; } = new();
    public Dictionary<string, Money> ProductPrices { get; } = new();
    public Dictionary<string, CouponReadModel> CouponReadModels { get; } = new();
    public List<ActivePromotionReadModel> ActivePromotionReadModels { get; } = new();
}

public sealed class InMemoryCouponRepository : ICouponRepository
{
    private readonly InMemoryOfferStore _store;

    public InMemoryCouponRepository(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task<Coupon?> GetAsync(string couponCode, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Coupons.GetValueOrDefault(couponCode));

    public Task<Coupon?> GetForUpdateAsync(string couponCode, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Coupons.GetValueOrDefault(couponCode));

    public Task AddAsync(Coupon coupon, CancellationToken cancellationToken)
    {
        _store.Coupons[coupon.CouponCode] = coupon;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryCouponRedemptionRepository : ICouponRedemptionRepository
{
    private readonly InMemoryOfferStore _store;

    public InMemoryCouponRedemptionRepository(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task AddAsync(CouponRedemption redemption, CancellationToken cancellationToken)
    {
        _store.Redemptions.Add(redemption);
        return Task.CompletedTask;
    }

    public Task<CouponRedemption?> GetAsync(string couponCode, string orderId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Redemptions.FirstOrDefault(r => r.CouponCode == couponCode && r.OrderId == orderId));

    public Task<int> CountActiveByUserAsync(string couponCode, string userId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Redemptions.Count(r => r.CouponCode == couponCode && r.UserId == userId && r.VoidedAt is null));

    public Task<IReadOnlyList<CouponRedemption>> GetActiveByOrderIdAsync(string orderId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CouponRedemption>>(_store.Redemptions.Where(r => r.OrderId == orderId && r.VoidedAt is null).ToList());
}

public sealed class InMemoryPricingQuoteRepository : IPricingQuoteRepository
{
    private readonly InMemoryOfferStore _store;

    public InMemoryPricingQuoteRepository(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task AddAsync(PricingQuote quote, CancellationToken cancellationToken)
    {
        _store.Quotes.Add(quote);
        return Task.CompletedTask;
    }
}

public sealed class InMemoryPromotionCampaignRepository : IPromotionCampaignRepository
{
    private readonly InMemoryOfferStore _store;

    public InMemoryPromotionCampaignRepository(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task<PromotionCampaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Campaigns.GetValueOrDefault(campaignId));

    public Task<PromotionCampaign?> GetForUpdateAsync(Guid campaignId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Campaigns.GetValueOrDefault(campaignId));

    public Task<IReadOnlyList<PromotionCampaign>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PromotionCampaign>>(_store.Campaigns.Values.Where(c => c.IsActive(now)).ToList());

    public Task AddAsync(PromotionCampaign campaign, CancellationToken cancellationToken)
    {
        _store.Campaigns[campaign.Id] = campaign;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryProductPriceCache : IProductPriceCache
{
    private readonly InMemoryOfferStore _store;

    public InMemoryProductPriceCache(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task UpsertAsync(string sku, Money price, DateTimeOffset now, CancellationToken cancellationToken)
    {
        _store.ProductPrices[sku] = price;
        return Task.CompletedTask;
    }

    public Task<Money?> GetAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(_store.ProductPrices.GetValueOrDefault(sku));
}

public sealed class InMemoryCouponReadRepository : ICouponReadRepository
{
    private readonly InMemoryOfferStore _store;

    public InMemoryCouponReadRepository(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task<CouponReadModel?> GetAsync(string couponCode, CancellationToken cancellationToken) =>
        Task.FromResult(_store.CouponReadModels.GetValueOrDefault(couponCode));
}

public sealed class InMemoryPromotionReadRepository : IPromotionReadRepository
{
    private readonly InMemoryOfferStore _store;

    public InMemoryPromotionReadRepository(InMemoryOfferStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<ActivePromotionReadModel>> GetActiveAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ActivePromotionReadModel>>(
            _store.ActivePromotionReadModels.Where(p => now >= p.StartsAt && now < p.EndsAt).ToList());
}

public sealed class NoOpUnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class TestCurrentPrincipal : ICurrentPrincipal
{
    public string ActingPrincipal => "test-service-principal";
}
