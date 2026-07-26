using Kart.Shared.Domain;

namespace KartOfferService.Domain.Pricing;

/// <summary>api-contract.yaml's `Money` schema - amount + ISO currency code. Shared by PricingQuote's total and PromotionCampaign's DiscountRule.</summary>
public sealed record Money(decimal Amount, string Currency)
{
    public static Result<Money> Create(decimal amount, string currency)
    {
        if (amount < 0)
        {
            return Result.Failure<Money>(Error.Validation("Money amount cannot be negative."));
        }

        var normalizedCurrency = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalizedCurrency.Length != 3)
        {
            return Result.Failure<Money>(Error.Validation("Money currency must be a 3-letter ISO code."));
        }

        return Result.Success(new Money(amount, normalizedCurrency));
    }

    public Money Add(Money other)
    {
        if (other.Currency != Currency)
        {
            throw new InvalidOperationException($"Cannot add {other.Currency} to a {Currency} amount.");
        }

        return this with { Amount = Amount + other.Amount };
    }

    /// <summary>Clamped at zero - a discount can never push a total negative.</summary>
    public Money Subtract(decimal amount) => this with { Amount = Math.Max(0, Amount - amount) };

    public Money MultiplyBy(decimal factor) => this with { Amount = Math.Max(0, Amount * factor) };
}
