using KartOfferService.Api.Common;
using KartOfferService.Application.Features.GetPricingQuote;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace KartOfferService.Api.Controllers;

[ApiController]
[Route("v1/pricing")]
public sealed class PricingController : ControllerBase
{
    private readonly ISender _sender;

    public PricingController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>OFF-8: api-contract.yaml `POST /v1/pricing/quote` - checkout-path. Self-contained (architecture.md); publishes `PriceQuoteIssued`.</summary>
    [HttpPost("quote")]
    [ProducesResponseType(typeof(PricingQuoteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PricingQuoteResponse>> GetQuote([FromBody] GetPricingQuoteRequest request, CancellationToken cancellationToken)
    {
        var items = request.Items.Select(i => new PricingLineItem(i.Sku, i.Quantity)).ToList();
        var result = await _sender.Send(new GetPricingQuoteCommand(items, request.Currency), cancellationToken);
        return this.ToActionResult<PricingQuoteResponse, PricingQuoteResponse>(result, dto => Ok(dto));
    }
}

/// <summary>api-contract.yaml `getPricingQuote` requestBody shape.</summary>
public sealed record GetPricingQuoteRequest(IReadOnlyList<PricingLineItemRequest> Items, string Currency);

public sealed record PricingLineItemRequest(string Sku, int Quantity);
