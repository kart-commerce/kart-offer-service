using System.IdentityModel.Tokens.Jwt;
using KartOfferService.Application.Common;
using KartOfferService.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace KartOfferService.Infrastructure.Security;

/// <summary>
/// Resolves the acting principal from the caller's Identity-issued access token `sub` claim
/// (kart-identity-service's JwtAccessTokenGenerator) - the checkout caller's own subject for
/// customer-facing endpoints, or Admin Service's client-credentials service principal for
/// admin-only writes (ADR-0010/ADR-0019). Falls back to a well-known system id outside an HTTP
/// request context (event-consumer-triggered handlers stamp their own <see cref="SystemPrincipals"/> directly instead).
/// </summary>
public sealed class HttpCurrentPrincipal : ICurrentPrincipal
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentPrincipal(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string ActingPrincipal =>
        _httpContextAccessor.HttpContext?.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
        ?? SystemPrincipals.Unknown;
}
