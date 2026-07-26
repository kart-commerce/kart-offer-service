using Kart.Shared.Domain;
using KartOfferService.Domain.Common;

namespace KartOfferService.Domain.Pricing;

/// <summary>
/// ddd-model.md's PricingQuote aggregate root - an immutable snapshot of "what this cart costs,
/// right now" (identified by <see cref="AggregateRoot.Id"/> = QuoteId). Reflects price/promotion
/// state at the moment it was issued; never mutated, never retroactively recomputed
/// (requirement-spec.md's "never retroactive" domain invariant). Expires 15 minutes after issuance
/// (ddd-model.md Modeling Decision #2, a BRD-silent default) - a caller holding an expired quote
/// must request a fresh one, never reuse or silently extend it (edge-cases.md #5).
/// </summary>
public sealed class PricingQuote : AggregateRoot, IHasDomainEvents
{
    private static readonly TimeSpan QuoteTtl = TimeSpan.FromMinutes(15);

    public Money Total { get; private set; } = null!;
    public DateTimeOffset IssuedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; private set; } = string.Empty;
    public string UpdatedBy { get; private set; } = string.Empty;

    private PricingQuote()
    {
    }

    /// <summary>OFF-8: issues a new, immutable quote for the given computed total.</summary>
    public static PricingQuote Issue(Money total, string actingPrincipal, DateTimeOffset now)
    {
        var quote = new PricingQuote
        {
            Id = Guid.NewGuid(),
            Total = total,
            IssuedAt = now,
            ExpiresAt = now.Add(QuoteTtl),
            UpdatedAt = now,
            CreatedBy = actingPrincipal,
            UpdatedBy = actingPrincipal,
        };

        quote.Raise(new PriceQuoteIssuedDomainEvent(quote.Id, quote.Total, quote.ExpiresAt, now));
        return quote;
    }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
}
