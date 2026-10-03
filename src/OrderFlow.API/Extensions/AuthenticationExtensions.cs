using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.API.Authorization;
using OrderFlow.API.Security;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Infrastructure.Authentication;

namespace OrderFlow.API.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// JWT bearer authentication plus the role policies. Authorization is secure by default:
    /// the fallback policy requires an authenticated user, so an endpoint only becomes public
    /// by an explicit <c>[AllowAnonymous]</c>.
    /// </summary>
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // Configured via DI so it sees the already-validated JwtOptions singleton.
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtOptions>((bearer, jwt) =>
            {
                // Keep claim names exactly as issued ("sub", "role", ...); no legacy URI mapping.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),

                    // Pin the algorithm so a token cannot pick a weaker one ("alg" confusion).
                    ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),

                    NameClaimType = "sub",
                    RoleClaimType = JwtTokenGenerator.RoleClaim,
                };
            });

        services.AddAuthorizationBuilder().AddOrderFlowPolicies();

        return services;
    }
}
