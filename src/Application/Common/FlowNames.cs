namespace KartOfferService.Application.Common;

/// <summary>Business-flow tags for KartFlowContext.Push, per kart-conventions.md's per-flow tracing/logging standard.</summary>
public static class FlowNames
{
    /// <summary>business-flows.md flow #1's "Coupon" checkout step - Validate/Redeem coupon calls made from checkout.</summary>
    public const string NormalShoppingPurchaseJourney = "NormalShoppingPurchaseJourney";

    /// <summary>business-flows.md flow #12 - Create/Deactivate Coupon, Create/Deactivate Promotion Campaign, and the coupon usage-tracking lifecycle (redemption void on order cancellation).</summary>
    public const string OffersCouponsPromotionsManagementAdmin = "OffersCouponsPromotionsManagementAdmin";
}
