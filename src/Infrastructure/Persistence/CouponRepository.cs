using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Coupons;
using Microsoft.EntityFrameworkCore;

namespace KartOfferService.Infrastructure.Persistence;

public sealed class CouponRepository : ICouponRepository
{
    private readonly OfferDbContext _dbContext;

    public CouponRepository(OfferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Coupon?> GetAsync(string couponCode, CancellationToken cancellationToken) =>
        _dbContext.Coupons.FirstOrDefaultAsync(c => c.CouponCode == couponCode, cancellationToken);

    public Task<Coupon?> GetForUpdateAsync(string couponCode, CancellationToken cancellationToken) =>
        _dbContext.Coupons
            .FromSqlInterpolated($"SELECT * FROM coupons WHERE coupon_code = {couponCode} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Coupon coupon, CancellationToken cancellationToken) =>
        await _dbContext.Coupons.AddAsync(coupon, cancellationToken);
}
