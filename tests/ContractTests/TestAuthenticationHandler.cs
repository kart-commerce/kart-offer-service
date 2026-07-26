using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KartOfferService.ContractTests;

/// <summary>
/// Stands in for real Identity-issued JWT validation in contract tests - exercises the actual
/// `[Authorize(Policy = "AdminOnly")]` pipeline against a caller-supplied `X-Test-Roles` header
/// instead of fetching/validating a real RS256 token, so these tests can assert both the 2xx
/// (admin) and 403 (non-admin) paths api-contract.yaml documents without a live Identity service.
/// </summary>
public sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string RolesHeader = "X-Test-Roles";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RolesHeader, out var rolesHeader))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = rolesHeader
            .SelectMany(value => (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
            .Select(role => new Claim("roles", role))
            .Append(new Claim(JwtRegisteredClaimNames.Sub, "test-service-principal"))
            .ToList();

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
