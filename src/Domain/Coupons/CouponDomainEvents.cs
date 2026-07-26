using Kart.Shared.Domain;

namespace KartOfferService.Domain.Coupons;

/// <summary>event-contract.md `CouponIssued` - fired only on explicit `POST /coupons` (ddd-model.md).</summary>
public sealed record CouponIssuedDomainEvent(
    string CouponCode,
    int? PerUserCap,
    int? GlobalCap,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>event-contract.md `CouponRedeemed` - key fields `code`, `orderId`.</summary>
public sealed record CouponRedeemedDomainEvent(
    string CouponCode,
    string UserId,
    string OrderId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>event-contract.md `CouponRedemptionVoided` - fired when OFF-3 consumes `OrderCancelled`.</summary>
public sealed record CouponRedemptionVoidedDomainEvent(
    string CouponCode,
    string OrderId,
    DateTimeOffset VoidedAt,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>
/// event-contract.md `CouponDeactivated` - fired only on explicit deactivation, never on natural
/// `validityWindow` expiry (ddd-model.md Modeling Decision #4).
/// </summary>
public sealed record CouponDeactivatedDomainEvent(
    string CouponCode,
    DateTimeOffset DeactivatedAt,
    DateTimeOffset OccurredAt) : IDomainEvent;
