using FluentAssertions;
using KartOfferService.Domain.Pricing;
using Xunit;

namespace KartOfferService.UnitTests.Domain;

public class PricingQuoteTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Issue_SetsExpiryToFifteenMinutesAfterIssuance_AndRaisesEvent()
    {
        var total = Money.Create(99.99m, "USD").Value;

        var quote = PricingQuote.Issue(total, "checkout-caller", Now);

        quote.IssuedAt.Should().Be(Now);
        quote.ExpiresAt.Should().Be(Now.AddMinutes(15));
        quote.Total.Should().Be(total);
        quote.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<PriceQuoteIssuedDomainEvent>();
    }

    [Fact]
    public void IsExpired_BeforeExpiry_IsFalse()
    {
        var quote = PricingQuote.Issue(Money.Create(10, "USD").Value, "caller", Now);

        quote.IsExpired(Now.AddMinutes(14)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_AtOrAfterExpiry_IsTrue()
    {
        var quote = PricingQuote.Issue(Money.Create(10, "USD").Value, "caller", Now);

        quote.IsExpired(Now.AddMinutes(15)).Should().BeTrue();
        quote.IsExpired(Now.AddMinutes(16)).Should().BeTrue();
    }
}
