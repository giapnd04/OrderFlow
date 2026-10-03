using System.Text;
using Microsoft.Extensions.Configuration;

namespace OrderFlow.Infrastructure.Authentication;

/// <summary>
/// JWT settings, read from the <c>Jwt</c> configuration section. The signing key is a secret:
/// it is never committed to <c>appsettings.json</c> (supply it via user-secrets or the
/// <c>Jwt__SigningKey</c> environment variable). Invalid settings fail at startup, not on
/// the first login.
/// </summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs a key of at least 256 bits.</summary>
    public const int MinSigningKeyBytes = 32;

    public string Issuer { get; init; } = "OrderFlow";

    public string Audience { get; init; } = "OrderFlow.Api";

    public string SigningKey { get; init; } = string.Empty;

    public int ExpiryMinutes { get; init; } = 60;

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(SectionName);

        var options = new JwtOptions
        {
            Issuer = section["Issuer"] is { Length: > 0 } issuer ? issuer : "OrderFlow",
            Audience = section["Audience"] is { Length: > 0 } audience ? audience : "OrderFlow.Api",
            SigningKey = section["SigningKey"] ?? string.Empty,
            ExpiryMinutes = int.TryParse(section["ExpiryMinutes"], out var minutes) ? minutes : 60,
        };

        options.Validate();

        return options;
    }

    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(SigningKey) < MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"Configuration '{SectionName}:SigningKey' is missing or shorter than {MinSigningKeyBytes} bytes. " +
                "Set it with user-secrets or the Jwt__SigningKey environment variable.");
        }

        if (ExpiryMinutes is < 1 or > 1440)
        {
            throw new InvalidOperationException($"Configuration '{SectionName}:ExpiryMinutes' must be between 1 and 1440.");
        }
    }
}
