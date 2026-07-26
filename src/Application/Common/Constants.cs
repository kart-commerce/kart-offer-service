namespace KartOfferService.Application.Common;

public static class Constants
{
    /// <summary>See <see cref="Features.RecomputeCatalogPrice.RecomputeCatalogPriceCommandHandler"/>'s remarks - `ProductPriceChanged` carries no currency field.</summary>
    public const string DefaultCurrency = "USD";
}
