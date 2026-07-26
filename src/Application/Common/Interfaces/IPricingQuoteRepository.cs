using KartOfferService.Domain.Pricing;

namespace KartOfferService.Application.Common.Interfaces;

/// <summary>Write-side (PostgreSQL) access to the append-only PricingQuote aggregate.</summary>
public interface IPricingQuoteRepository
{
    Task AddAsync(PricingQuote quote, CancellationToken cancellationToken);
}
