using FluentAssertions;
using KartOfferService.Domain.Pricing;
using Xunit;

namespace KartOfferService.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Create_WithNegativeAmount_Fails()
    {
        var result = Money.Create(-1, "USD");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("validation_error");
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDD")]
    [InlineData("")]
    public void Create_WithInvalidCurrencyCode_Fails(string currency)
    {
        var result = Money.Create(10, currency);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Create_NormalizesCurrencyToUppercase()
    {
        var result = Money.Create(10, "usd");

        result.Value.Currency.Should().Be("USD");
    }

    [Fact]
    public void Subtract_NeverGoesBelowZero()
    {
        var money = new Money(10, "USD");

        var result = money.Subtract(50);

        result.Amount.Should().Be(0);
    }

    [Fact]
    public void MultiplyBy_AppliesFactor()
    {
        var money = new Money(100, "USD");

        var result = money.MultiplyBy(0.9m);

        result.Amount.Should().Be(90);
    }

    [Fact]
    public void Add_WithMismatchedCurrency_Throws()
    {
        var usd = new Money(10, "USD");
        var eur = new Money(5, "EUR");

        var act = () => usd.Add(eur);

        act.Should().Throw<InvalidOperationException>();
    }
}
