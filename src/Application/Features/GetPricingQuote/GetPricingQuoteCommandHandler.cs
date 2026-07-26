using Kart.Shared.Domain;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Application.Common.Models;
using KartOfferService.Domain.Pricing;
using MediatR;

namespace KartOfferService.Application.Features.GetPricingQuote;

/// <summary>
/// architecture.md's key constraint: this endpoint must be self-contained to hold P95&lt;150ms -
/// both catalog price (<see cref="IProductPriceCache"/>) and active campaigns
/// (<see cref="IPromotionCampaignRepository"/>) are read locally/in-process, never via a
/// synchronous call to Product or a round trip through the eventually-consistent Mongo read model.
/// ddd-model.md Modeling Decision #3: best-discount-wins, no stacking.
/// </summary>
public sealed class GetPricingQuoteCommandHandler : IRequestHandler<GetPricingQuoteCommand, Result<PricingQuoteResponse>>
{
    private readonly IProductPriceCache _priceCache;
    private readonly IPromotionCampaignRepository _campaigns;
    private readonly IPricingQuoteRepository _quotes;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentPrincipal _currentPrincipal;
    private readonly TimeProvider _timeProvider;

    public GetPricingQuoteCommandHandler(
        IProductPriceCache priceCache,
        IPromotionCampaignRepository campaigns,
        IPricingQuoteRepository quotes,
        IUnitOfWork unitOfWork,
        ICurrentPrincipal currentPrincipal,
        TimeProvider timeProvider)
    {
        _priceCache = priceCache;
        _campaigns = campaigns;
        _quotes = quotes;
        _unitOfWork = unitOfWork;
        _currentPrincipal = currentPrincipal;
        _timeProvider = timeProvider;
    }

    public async Task<Result<PricingQuoteResponse>> Handle(GetPricingQuoteCommand request, CancellationToken cancellationToken)
    {
        decimal subtotalAmount = 0;
        foreach (var item in request.Items)
        {
            var price = await _priceCache.GetAsync(item.Sku, cancellationToken);
            if (price is null)
            {
                return Result.Failure<PricingQuoteResponse>(
                    Error.Validation($"No known price for sku '{item.Sku}' - it has never been observed via a ProductPriceChanged event."));
            }

            subtotalAmount += price.Amount * item.Quantity;
        }

        var subtotalResult = Money.Create(subtotalAmount, request.Currency);
        if (subtotalResult.IsFailure)
        {
            return Result.Failure<PricingQuoteResponse>(subtotalResult.Error);
        }

        var subtotal = subtotalResult.Value;
        var now = _timeProvider.GetUtcNow();
        var activeCampaigns = await _campaigns.GetActiveAsync(now, cancellationToken);

        var best = activeCampaigns
            .Select(c => c.DiscountRule.Apply(subtotal))
            .Append(subtotal)
            .MinBy(m => m.Amount)!;

        var quote = PricingQuote.Issue(best, _currentPrincipal.ActingPrincipal, now);
        await _quotes.AddAsync(quote, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new PricingQuoteResponse(quote.Id, new MoneyDto(quote.Total.Amount, quote.Total.Currency), quote.ExpiresAt));
    }
}
