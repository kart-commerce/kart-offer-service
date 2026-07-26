using Kart.Shared.Domain;
using KartOfferService.Domain.Common;

namespace KartOfferService.Domain.Coupons;

/// <summary>
/// ddd-model.md's Coupon aggregate root - identified by <see cref="CouponCode"/>, a natural
/// (caller-supplied, globally-unique) string key, not a synthetic Guid. This is why Coupon does
/// not inherit <see cref="Kart.Shared.Domain.AggregateRoot"/> (which forces a <c>Guid Id</c>) -
/// it raises the same <see cref="IDomainEvent"/>-collecting shape locally instead, since a
/// synthetic Guid identity would misrepresent this aggregate's true key
/// (database-design.md: `coupon_code TEXT PRIMARY KEY`).
///
/// Deactivation is modeled as early-truncating <see cref="ValidUntil"/>, never a separate status
/// flag (ddd-model.md Modeling Decision #4) - a coupon past its window is simply expired, whether
/// that happened naturally or via explicit deactivation; only the latter publishes
/// <see cref="CouponDeactivatedDomainEvent"/>.
/// </summary>
public sealed class Coupon : IHasDomainEvents
{
    private readonly List<IDomainEvent> _domainEvents = new();

    public string CouponCode { get; private set; } = string.Empty;
    public int? PerUserCap { get; private set; }
    public int? GlobalCap { get; private set; }
    public DateTimeOffset ValidFrom { get; private set; }
    public DateTimeOffset ValidUntil { get; private set; }
    public int TotalRedemptions { get; private set; }
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    private RedemptionLimit Limit => new(PerUserCap, GlobalCap, ValidFrom, ValidUntil);

    // EF Core materialization only.
    private Coupon()
    {
    }

    private Coupon(
        string couponCode,
        int? perUserCap,
        int? globalCap,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        string actingPrincipal,
        DateTimeOffset now)
    {
        CouponCode = couponCode;
        PerUserCap = perUserCap;
        GlobalCap = globalCap;
        ValidFrom = validFrom;
        ValidUntil = validUntil;
        TotalRedemptions = 0;
        Version = 1;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = actingPrincipal;
        UpdatedBy = actingPrincipal;
    }

    /// <summary>OFF-9: issues a brand-new coupon. Global uniqueness is enforced by the `coupon_code` PRIMARY KEY, not here.</summary>
    public static Result<Coupon> Issue(
        string couponCode,
        int? perUserCap,
        int? globalCap,
        DateTimeOffset validFrom,
        DateTimeOffset validUntil,
        string actingPrincipal,
        DateTimeOffset now)
    {
        var normalizedCode = couponCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(normalizedCode))
        {
            return Result.Failure<Coupon>(Error.Validation("couponCode is required."));
        }

        if (validUntil <= validFrom)
        {
            return Result.Failure<Coupon>(Error.Validation("validUntil must be after validFrom."));
        }

        if (perUserCap is <= 0 || globalCap is <= 0)
        {
            return Result.Failure<Coupon>(Error.Validation("perUserCap and globalCap must be positive when specified."));
        }

        var coupon = new Coupon(normalizedCode, perUserCap, globalCap, validFrom, validUntil, actingPrincipal, now);
        coupon._domainEvents.Add(new CouponIssuedDomainEvent(coupon.CouponCode, perUserCap, globalCap, validFrom, validUntil, now));
        return Result.Success(coupon);
    }

    /// <summary>
    /// OFF-1: read-only validity check against redemption rules - never mutates state, never
    /// raises an event. Used both by the standalone validate endpoint and as the first check
    /// inside <see cref="Redeem"/> itself.
    /// </summary>
    public Result CanRedeem(int userRedemptionsSoFar, DateTimeOffset now)
    {
        if (!Limit.IsWithinWindow(now))
        {
            return Result.Failure(Error.Custom("coupon_expired", $"Coupon '{CouponCode}' is not within its valid window."));
        }

        if (!Limit.HasGlobalCapacity(TotalRedemptions))
        {
            return Result.Failure(Error.Conflict($"Coupon '{CouponCode}' has reached its global redemption cap."));
        }

        if (!Limit.HasPerUserCapacity(userRedemptionsSoFar))
        {
            return Result.Failure(Error.Conflict($"Coupon '{CouponCode}' has reached its per-user redemption cap for this user."));
        }

        return Result.Success();
    }

    /// <summary>
    /// OFF-2: redeems this coupon for an order. Caller is responsible for locking this aggregate's
    /// row (`SELECT ... FOR UPDATE`, design-decisions.md "Concurrency Control for Coupon") and for
    /// the real double-redemption guard, the `(coupon_code, order_id)` unique constraint on
    /// `coupon_redemptions` (edge-cases.md #1) - the window/cap re-check here is what that same
    /// lock protects against a racing redeem/deactivate (edge-cases.md #2, #3, #7).
    /// </summary>
    public Result<CouponRedemption> Redeem(string userId, string orderId, int userRedemptionsSoFar, string actingPrincipal, DateTimeOffset now)
    {
        var canRedeem = CanRedeem(userRedemptionsSoFar, now);
        if (canRedeem.IsFailure)
        {
            return Result.Failure<CouponRedemption>(canRedeem.Error);
        }

        TotalRedemptions++;
        Touch(actingPrincipal, now);

        var redemption = CouponRedemption.Create(CouponCode, userId, orderId, now, actingPrincipal);
        _domainEvents.Add(new CouponRedeemedDomainEvent(CouponCode, userId, orderId, now));
        return Result.Success(redemption);
    }

    /// <summary>
    /// OFF-3: reverses a redemption's effect on this coupon's cap accounting when the order it was
    /// applied to is cancelled (consumes `OrderCancelled`). Never retroactively invalidates the
    /// completed redemption record itself - only <see cref="CouponRedemption.Void"/> does that,
    /// and only the cap counter here is adjusted so the freed slot becomes redeemable again.
    /// </summary>
    public void VoidRedemption(string orderId, string actingPrincipal, DateTimeOffset now)
    {
        if (TotalRedemptions > 0)
        {
            TotalRedemptions--;
        }

        Touch(actingPrincipal, now);
        _domainEvents.Add(new CouponRedemptionVoidedDomainEvent(CouponCode, orderId, now, now));
    }

    /// <summary>
    /// requirement-spec.md item 6 (OFF-10): early-truncates <see cref="ValidUntil"/> to `now`,
    /// never later than the existing value. Idempotent - deactivating an already-expired/deactivated
    /// coupon succeeds without raising a second <see cref="CouponDeactivatedDomainEvent"/>.
    /// </summary>
    public void Deactivate(string actingPrincipal, DateTimeOffset now)
    {
        if (now >= ValidUntil)
        {
            return;
        }

        ValidUntil = now;
        Touch(actingPrincipal, now);
        _domainEvents.Add(new CouponDeactivatedDomainEvent(CouponCode, now, now));
    }

    public void ClearDomainEvents() => _domainEvents.Clear();

    private void Touch(string actingPrincipal, DateTimeOffset now)
    {
        Version++;
        UpdatedAt = now;
        UpdatedBy = actingPrincipal;
    }
}
