using Kart.Shared.Domain;
using MediatR;

namespace KartOfferService.Application.Features.VoidCouponRedemption;

/// <summary>OFF-3: consumes `OrderCancelled` (orderId, reason) - publishes `CouponRedemptionVoided` for every active redemption tied to that order, if any.</summary>
public sealed record VoidCouponRedemptionCommand(string OrderId) : IRequest<Result>;
