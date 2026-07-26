using Kart.Shared.Domain;
using KartOfferService.Domain.Pricing;

namespace KartOfferService.Domain.Promotions;

/// <summary>
/// ddd-model.md's PromotionCampaign value object - how much a campaign discounts a subtotal.
/// Modeled as a small Strategy hierarchy (coding-standards.md's design-pattern table: "Strategy for
/// pricing/discount-per-type") rather than a single class with a "kind" enum and a branch per
/// case - database-design.md's `discount_rule JSONB` column stores whichever concrete shape this
/// resolves to (Infrastructure owns the JSON <-> type mapping, not Domain).
/// </summary>
public abstract record DiscountRule
{
    public abstract Money Apply(Money subtotal);
}

public sealed record PercentageOffDiscountRule(decimal PercentageOff) : DiscountRule
{
    public static Result<PercentageOffDiscountRule> Create(decimal percentageOff)
    {
        if (percentageOff is <= 0 or > 100)
        {
            return Result.Failure<PercentageOffDiscountRule>(Error.Validation("percentageOff must be between 0 (exclusive) and 100 (inclusive)."));
        }

        return Result.Success(new PercentageOffDiscountRule(percentageOff));
    }

    public override Money Apply(Money subtotal) => subtotal.MultiplyBy(1 - (PercentageOff / 100m));
}

public sealed record FixedAmountOffDiscountRule(decimal AmountOff) : DiscountRule
{
    public static Result<FixedAmountOffDiscountRule> Create(decimal amountOff)
    {
        if (amountOff <= 0)
        {
            return Result.Failure<FixedAmountOffDiscountRule>(Error.Validation("amountOff must be positive."));
        }

        return Result.Success(new FixedAmountOffDiscountRule(amountOff));
    }

    public override Money Apply(Money subtotal) => subtotal.Subtract(AmountOff);
}
