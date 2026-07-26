using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using MongoDB.Driver;

namespace KartOfferService.Infrastructure.Persistence.ReadModel;

public sealed class CouponReadRepository : ICouponReadRepository
{
    private readonly OfferReadDbContext _context;

    public CouponReadRepository(OfferReadDbContext context)
    {
        _context = context;
    }

    public async Task<CouponReadModel?> GetAsync(string couponCode, CancellationToken cancellationToken)
    {
        var document = await _context.Coupons.Find(d => d.Id == couponCode).FirstOrDefaultAsync(cancellationToken);
        return document is null
            ? null
            : new CouponReadModel(
                document.Id,
                document.PerUserCap,
                document.GlobalCap,
                new DateTimeOffset(document.ValidFrom, TimeSpan.Zero),
                new DateTimeOffset(document.ValidUntil, TimeSpan.Zero),
                document.TotalRedemptions);
    }
}
