using FluentAssertions;
using KartOfferService.Domain.Pricing;
using KartOfferService.Domain.Promotions;
using Xunit;

namespace KartOfferService.UnitTests.Domain;

public class DiscountRuleTests
{
    [Fact]
    public void PercentageOff_Apply_ReducesTotalByPercentage()
    {
        var rule = PercentageOffDiscountRule.Create(10).Value;
        var subtotal = new Money(100, "USD");

        var result = rule.Apply(subtotal);

        result.Amount.Should().Be(90);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(101)]
    public void PercentageOff_Create_RejectsOutOfRangeValues(decimal percentage)
    {
        var result = PercentageOffDiscountRule.Create(percentage);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void FixedAmountOff_Apply_SubtractsFlatAmount_ClampedAtZero()
    {
        var rule = FixedAmountOffDiscountRule.Create(15).Value;

        rule.Apply(new Money(100, "USD")).Amount.Should().Be(85);
        rule.Apply(new Money(10, "USD")).Amount.Should().Be(0);
    }

    [Fact]
    public void FixedAmountOff_Create_RejectsNonPositiveValues()
    {
        FixedAmountOffDiscountRule.Create(0).IsFailure.Should().BeTrue();
        FixedAmountOffDiscountRule.Create(-1).IsFailure.Should().BeTrue();
    }
}
