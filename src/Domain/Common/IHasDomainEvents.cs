using Kart.Shared.Domain;

namespace KartOfferService.Domain.Common;

/// <summary>
/// Common shape <see cref="Coupons.Coupon"/> (which cannot inherit <see cref="AggregateRoot"/> -
/// its identity is a natural string key, not a Guid) and every <see cref="AggregateRoot"/>-derived
/// aggregate (<see cref="Pricing.PricingQuote"/>, <see cref="Promotions.PromotionCampaign"/>)
/// already satisfy implicitly. Lets Infrastructure's DbContext convert any tracked entity's pending
/// domain events into outbox rows with one `ChangeTracker.Entries&lt;IHasDomainEvents&gt;()` scan,
/// regardless of which base type (if any) the entity uses.
/// </summary>
public interface IHasDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }

    void ClearDomainEvents();
}
