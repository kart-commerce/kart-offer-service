using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Pricing;

namespace KartOfferService.Infrastructure.Persistence;

public sealed class PricingQuoteRepository : IPricingQuoteRepository
{
    private readonly OfferDbContext _dbContext;

    public PricingQuoteRepository(OfferDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(PricingQuote quote, CancellationToken cancellationToken) =>
        await _dbContext.PricingQuotes.AddAsync(quote, cancellationToken);
}
