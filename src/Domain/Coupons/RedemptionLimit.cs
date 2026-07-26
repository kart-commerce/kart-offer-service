namespace KartOfferService.Domain.Coupons;

/// <summary>
/// ddd-model.md's Coupon value object - per-user cap, global cap, and validity window, each
/// independently optional (Modeling Decision #1). Derived on demand from Coupon's own flat
/// columns (database-design.md keeps these as plain columns, not a JSONB blob) rather than
/// persisted as its own row/column - a pure in-memory computation helper.
/// </summary>
public sealed record RedemptionLimit(int? PerUserCap, int? GlobalCap, DateTimeOffset ValidFrom, DateTimeOffset ValidUntil)
{
    public bool IsWithinWindow(DateTimeOffset now) => now >= ValidFrom && now < ValidUntil;

    public bool HasGlobalCapacity(int totalRedemptions) => GlobalCap is null || totalRedemptions < GlobalCap;

    public bool HasPerUserCapacity(int userRedemptionsSoFar) => PerUserCap is null || userRedemptionsSoFar < PerUserCap;
}
