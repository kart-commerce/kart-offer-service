using KartOfferService.Domain.Coupons;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>
/// Write-side (PostgreSQL) access to the Coupon aggregate - the source of truth every money-adjacent
/// operation (redeem, admin issue/deactivate) reads and writes through. Never used for the
/// high-throughput checkout-path validate read - see <see cref="ICouponReadRepository"/> for that.
/// </summary>
public interface ICouponRepository
{
    Task<Coupon?> GetAsync(string couponCode, CancellationToken cancellationToken);

    /// <summary>
    /// `SELECT ... FOR UPDATE` (design-decisions.md "Concurrency Control for Coupon") - must run
    /// inside an <see cref="IUnitOfWork"/> transaction so the lock survives across the
    /// check-then-write that follows.
    /// </summary>
    Task<Coupon?> GetForUpdateAsync(string couponCode, CancellationToken cancellationToken);

    Task AddAsync(Coupon coupon, CancellationToken cancellationToken);
}
