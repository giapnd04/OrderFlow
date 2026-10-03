using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Authentication;

/// <summary>
/// Issues HS256 access tokens. Claim names are fixed here and mirrored by the API's
/// token validation (<c>role</c> is the role claim, <c>sub</c> the user id).
/// </summary>
public sealed class JwtTokenGenerator : IJwtTokenGenerator
{
    public const string RoleClaim = "role";

    public const string CustomerIdClaim = "customer_id";

    public const string EmailVerifiedClaim = "email_verified";

    private readonly JwtOptions _options;
    private readonly TimeProvider _clock;

    public JwtTokenGenerator(JwtOptions options, TimeProvider clock)
    {
        _options = options;
        _clock = clock;
    }

    public AccessToken Generate(User user)
    {
        var now = _clock.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(RoleClaim, user.Role.ToString()),
            new(EmailVerifiedClaim, user.IsEmailVerified ? "true" : "false"),
        };

        if (user.CustomerId is { } customerId)
        {
            claims.Add(new Claim(CustomerIdClaim, customerId.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            NotBefore = now,
            IssuedAt = now,
            Expires = expires,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
