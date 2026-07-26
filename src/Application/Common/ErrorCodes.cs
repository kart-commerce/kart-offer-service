namespace KartOfferService.Application.Common;

/// <summary>Service-specific error codes beyond `Kart.Shared.Domain.Error`'s generic vocabulary (validation_error/not_found/conflict/unauthorized).</summary>
public static class ErrorCodes
{
    /// <summary>
    /// api-contract.yaml's 412 responses on `deactivateCoupon`/`deactivatePromotionCampaign`:
    /// the caller's `If-Match` version no longer matches the current row
    /// (database-design.md's conditional `UPDATE ... WHERE version = $2` affected zero rows).
    /// </summary>
    public const string StaleVersion = "stale_version";

    /// <summary>edge-cases.md #3 - the coupon's validity window rejects a redemption attempt.</summary>
    public const string CouponExpired = "coupon_expired";
}
