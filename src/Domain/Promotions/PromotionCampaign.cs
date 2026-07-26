using Kart.Shared.Domain;
using KartOfferService.Domain.Common;

namespace KartOfferService.Domain.Promotions;

/// <summary>
/// ddd-model.md's PromotionCampaign aggregate root - identified by <see cref="AggregateRoot.Id"/>
/// = CampaignId. Deactivation is modeled as early-truncating <see cref="Window"/>'s end, never a
/// status flag (Modeling Decision #4) - symmetric with <see cref="Coupons.Coupon"/>'s own
/// deactivation mechanics. Precedence across multiple active campaigns is resolved by the
/// Application layer (best-discount-wins, no stacking - Modeling Decision #3); this aggregate only
/// owns its own window/discount-rule invariants.
/// </summary>
public sealed class PromotionCampaign : AggregateRoot, IHasDomainEvents
{
    public CampaignWindow Window { get; private set; } = null!;
    public DiscountRule DiscountRule { get; private set; } = null!;
    public int Version { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private PromotionCampaign()
    {
    }

    /// <summary>OFF-5: creates and immediately activates a new campaign.</summary>
    public static Result<PromotionCampaign> Create(
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DiscountRule discountRule,
        string actingPrincipal,
        DateTimeOffset now)
    {
        if (endsAt <= startsAt)
        {
            return Result.Failure<PromotionCampaign>(Error.Validation("endsAt must be after startsAt."));
        }

        var campaign = new PromotionCampaign
        {
            Id = Guid.NewGuid(),
            Window = new CampaignWindow(startsAt, endsAt),
            DiscountRule = discountRule,
            Version = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actingPrincipal,
            UpdatedBy = actingPrincipal,
        };

        campaign.Raise(new PromotionActivatedDomainEvent(campaign.Id, startsAt, endsAt, now));
        return Result.Success(campaign);
    }

    public bool IsActive(DateTimeOffset now) => Window.IsActive(now);

    /// <summary>OFF-6: early-truncates the campaign's window. Idempotent - a no-op past the point the campaign is already inactive.</summary>
    public void Deactivate(string actingPrincipal, DateTimeOffset now)
    {
        if (now >= Window.EndsAt)
        {
            return;
        }

        Window = Window with { EndsAt = now };
        Version++;
        UpdatedAt = now;
        UpdatedBy = actingPrincipal;
        Raise(new PromotionDeactivatedDomainEvent(Id, now, now));
    }
}
