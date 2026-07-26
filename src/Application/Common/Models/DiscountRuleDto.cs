using KartOfferService.Domain.Promotions;

namespace KartOfferService.Application.Common.Models;

/// <summary>
/// Wire shape for ddd-model.md's <see cref="DiscountRule"/> Strategy hierarchy - api-contract.yaml
/// models `discountRule` as `additionalProperties: true` (shape varies by campaign type), so this
/// is a small type-discriminated DTO rather than one shape per rule type on the wire.
/// </summary>
public sealed record DiscountRuleDto(string Type, decimal Value)
{
    public const string PercentageOffType = "percentageOff";
    public const string FixedAmountOffType = "fixedAmountOff";

    public static DiscountRuleDto FromDomain(DiscountRule rule) => rule switch
    {
        PercentageOffDiscountRule p => new DiscountRuleDto(PercentageOffType, p.PercentageOff),
        FixedAmountOffDiscountRule f => new DiscountRuleDto(FixedAmountOffType, f.AmountOff),
        _ => throw new InvalidOperationException($"No wire mapping for discount rule type {rule.GetType().Name}."),
    };
}
