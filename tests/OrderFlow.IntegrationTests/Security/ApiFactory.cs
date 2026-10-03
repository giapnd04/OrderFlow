using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Infrastructure.Authentication;

namespace OrderFlow.IntegrationTests.Security;

/// <summary>
/// Hosts the real API pipeline in memory. Every test here is DB-free: they only exercise
/// requests that are rejected (or answered) before any repository is touched, so the
/// connection string is a deliberately unreachable placeholder.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string SigningKey = "integration-test-signing-key-32-bytes-or-more!!";

    protected virtual IReadOnlyDictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing", not Development: the dev appsettings (LocalDB, dev key) must not leak in.
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:OrderFlow", "Server=127.0.0.1,1;Database=Unreachable;Connect Timeout=1");
        builder.UseSetting("Jwt:SigningKey", SigningKey);

        foreach (var (key, value) in ExtraSettings)
        {
            builder.UseSetting(key, value);
        }
    }
}

internal static class TestTokens
{
    private static readonly JwtOptions Options = new() { SigningKey = ApiFactory.SigningKey };

    public static string For(UserRole role, int? customerId = null, string signingKey = ApiFactory.SigningKey, TimeProvider? clock = null)
    {
        var user = User.Create($"{role}@example.com".ToLowerInvariant(), "hash", role);
        user.Id = 1;

        if (customerId is { } id)
        {
            user.LinkCustomer(id);
        }

        var options = signingKey == ApiFactory.SigningKey ? Options : new JwtOptions { SigningKey = signingKey };

        return new JwtTokenGenerator(options, clock ?? TimeProvider.System).Generate(user).Token;
    }
}
