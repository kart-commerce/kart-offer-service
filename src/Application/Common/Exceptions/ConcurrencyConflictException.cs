namespace KartOfferService.Application.Common.Exceptions;

/// <summary>
/// Thrown by Infrastructure when EF Core's own optimistic-concurrency token (the `version` column,
/// `IsConcurrencyToken()`) detects a write racing another write between a handler's own
/// version-match check and its `SaveChangesAsync` call - the database-enforced backstop behind
/// <see cref="Features.DeactivateCoupon.DeactivateCouponCommandHandler"/> and
/// <see cref="Features.DeactivatePromotionCampaign.DeactivatePromotionCampaignCommandHandler"/>'s
/// own pre-checks. Mapped to 412 via `Kart.Shared.ErrorHandling`'s exception-mapping registry.
/// </summary>
public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
