using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Coupons;
using Microsoft.EntityFrameworkCore;

namespace KartOfferService.Infrastructure.Persistence;

public sealed class CouponRedemptionRepository : ICouponRedemptionRepository
{
    private readonly OfferDbContext _dbContext;

    public CouponRedemptionRepository(OfferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(CouponRedemption redemption, CancellationToken cancellationToken) =>
        await _dbContext.CouponRedemptions.AddAsync(redemption, cancellationToken);

    public Task<CouponRedemption?> GetAsync(string couponCode, string orderId, CancellationToken cancellationToken) =>
        _dbContext.CouponRedemptions.FirstOrDefaultAsync(r => r.CouponCode == couponCode && r.OrderId == orderId, cancellationToken);

    public Task<int> CountActiveByUserAsync(string couponCode, string userId, CancellationToken cancellationToken) =>
        _dbContext.CouponRedemptions.CountAsync(
            r => r.CouponCode == couponCode && r.UserId == userId && r.VoidedAt == null, cancellationToken);

    public async Task<IReadOnlyList<CouponRedemption>> GetActiveByOrderIdAsync(string orderId, CancellationToken cancellationToken) =>
        await _dbContext.CouponRedemptions
            .Where(r => r.OrderId == orderId && r.VoidedAt == null)
            .ToListAsync(cancellationToken);
}
