using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace KartOfferService.Api.Security;

/// <summary>
/// api-contract.yaml's `clientCredentials` security scheme: an Identity-issued RS256 JWT, checked
/// structurally here (signature + expiry), never re-deriving role grants locally (BRD S24.1 -
/// Identity is the sole issuer of platform role claims). "AdminOnly" gates every RBAC-gated write
/// endpoint on kart-identity-service's actual claim shape: `new Claim("roles", role)`, value
/// "admin" for the Admin-scoped service principal Admin Service authenticates as (ADR-0010/ADR-0019).
/// </summary>
public static class AuthenticationExtensions
{
    public const string AdminPolicy = "AdminOnly";
    private const string RolesClaimType = "roles";
    private const string AdminRoleValue = "admin";

    public static IServiceCollection AddOfferAuthentication(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddHttpClient<JwksSigningKeyResolver>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwksSigningKeyResolver>((options, resolver) =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeyResolver = resolver.ResolveSigningKeys,
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireClaim(RolesClaimType, AdminRoleValue));

        return services;
    }
}
