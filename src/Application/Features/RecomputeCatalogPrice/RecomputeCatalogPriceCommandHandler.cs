using Kart.Shared.Domain;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using KartOfferService.Domain.Pricing;
using MediatR;

namespace KartOfferService.Application.Features.RecomputeCatalogPrice;

/// <summary>
/// architecture.md: "catalog price must be materialized locally from `ProductPriceChanged`, never
/// fetched synchronously." `ProductPriceChanged`'s own payload carries no currency
/// (event-contract.md), so every materialized price is assumed to already be in
/// <see cref="Constants.DefaultCurrency"/> - a documented scope simplification, not a defect
/// (this platform has no multi-currency catalog requirement stated anywhere in the approved docs).
/// </summary>
public sealed class RecomputeCatalogPriceCommandHandler : IRequestHandler<RecomputeCatalogPriceCommand, Result>
{
    private readonly IProductPriceCache _priceCache;
    private readonly TimeProvider _timeProvider;

    public RecomputeCatalogPriceCommandHandler(IProductPriceCache priceCache, TimeProvider timeProvider)
    {
        _priceCache = priceCache;
        _timeProvider = timeProvider;
    }

    public async Task<Result> Handle(RecomputeCatalogPriceCommand request, CancellationToken cancellationToken)
    {
        var moneyResult = Money.Create(request.NewPrice, Constants.DefaultCurrency);
        if (moneyResult.IsFailure)
        {
            return Result.Failure(moneyResult.Error);
        }

        await _priceCache.UpsertAsync(request.Sku, moneyResult.Value, _timeProvider.GetUtcNow(), cancellationToken);
        return Result.Success();
    }
}
