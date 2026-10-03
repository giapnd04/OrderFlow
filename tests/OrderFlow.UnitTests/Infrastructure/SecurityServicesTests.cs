using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderFlow.Application.Abstractions.Security;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Infrastructure.Authentication;
using OrderFlow.UnitTests.Application.Auth;

namespace OrderFlow.UnitTests.Infrastructure;

public sealed class SecurityServicesTests
{
    private const string Key = "unit-test-signing-key-at-least-32-bytes-long!!";

    private static readonly DateTime Now = new(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc);

    // Low iteration count: these tests check the format and behaviour, not the production cost.
    private static readonly Pbkdf2PasswordHasher Hasher = new(iterations: 1_000);

    [Fact]
    public void Hasher_RoundTrips()
    {
        var hash = Hasher.Hash("Password1");

        Assert.True(Hasher.Verify("Password1", hash));
        Assert.False(Hasher.Verify("Password2", hash));
        Assert.False(Hasher.Verify("password1", hash));
    }

    [Fact]
    public void Hasher_SaltsEveryHash_SoEqualPasswordsDifferInStorage()
    {
        Assert.NotEqual(Hasher.Hash("Password1"), Hasher.Hash("Password1"));
    }

    [Fact]
    public void Hasher_NeverStoresThePlainPassword()
    {
        Assert.DoesNotContain("Password1", Hasher.Hash("Password1"));
    }

    [Fact]
    public void Hasher_FormatIsSelfDescribing()
    {
        var parts = Hasher.Hash("Password1").Split('$');

        Assert.Equal(4, parts.Length);
        Assert.Equal("PBKDF2-SHA256", parts[0]);
        Assert.Equal("1000", parts[1]);
    }

    [Fact]
    public void Hasher_VerifiesHashesMadeWithADifferentIterationCount()
    {
        var oldHash = new Pbkdf2PasswordHasher(iterations: 500).Hash("Password1");

        Assert.True(Hasher.Verify("Password1", oldHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("PBKDF2-SHA256$abc$AAAA$AAAA")]
    [InlineData("PBKDF2-SHA256$1000$not-base64!$also-not")]
    [InlineData("OTHER$1000$AAAA$AAAA")]
    public void Hasher_MalformedStoredHash_FailsClosedInsteadOfThrowing(string stored)
    {
        Assert.False(Hasher.Verify("Password1", stored));
    }

    [Fact]
    public void Hasher_DefaultIterationsMeetTheOwaspMinimum()
    {
        Assert.True(Pbkdf2PasswordHasher.DefaultIterations >= 600_000);
    }

    [Fact]
    public void OtpGenerator_ProducesSixDigitNumericCodes()
    {
        var generator = new SecureOtpGenerator();

        for (var i = 0; i < 200; i++)
        {
            var code = generator.GenerateCode();

            Assert.Equal(6, code.Length);
            Assert.All(code, c => Assert.True(char.IsAsciiDigit(c)));
        }
    }

    [Fact]
    public void OtpGenerator_IsNotConstant()
    {
        var generator = new SecureOtpGenerator();

        var distinct = Enumerable.Range(0, 50).Select(_ => generator.GenerateCode()).Distinct().Count();

        Assert.True(distinct > 40, $"expected mostly distinct codes, got {distinct} distinct of 50");
    }

    private static User UserWith(UserRole role, int? customerId = null, bool verified = false)
    {
        var user = User.Create("alice@example.com", "hash", role, verified);
        user.Id = 42;

        if (customerId is { } id)
        {
            user.LinkCustomer(id);
        }

        return user;
    }

    private static JwtTokenGenerator Generator(string key = Key, int minutes = 30)
        => new(new JwtOptions { SigningKey = key, ExpiryMinutes = minutes }, new FixedTimeProvider(Now));

    private static JsonWebToken Read(AccessToken token) => new(token.Token);

    [Fact]
    public void Jwt_CarriesIdentityRoleAndCustomerClaims()
    {
        var token = Read(Generator().Generate(UserWith(UserRole.Customer, customerId: 7, verified: true)));

        Assert.Equal("42", token.GetClaim("sub").Value);
        Assert.Equal("alice@example.com", token.GetClaim("email").Value);
        Assert.Equal("Customer", token.GetClaim("role").Value);
        Assert.Equal("7", token.GetClaim("customer_id").Value);
        Assert.Equal("true", token.GetClaim("email_verified").Value);
        Assert.Equal("OrderFlow", token.Issuer);
        Assert.Contains("OrderFlow.Api", token.Audiences);
    }

    [Fact]
    public void Jwt_StaffTokenHasNoCustomerClaim()
    {
        var token = Read(Generator().Generate(UserWith(UserRole.Sales)));

        Assert.False(token.TryGetClaim("customer_id", out _));
        Assert.Equal("Sales", token.GetClaim("role").Value);
    }

    [Fact]
    public void Jwt_ExpiresAfterTheConfiguredLifetime()
    {
        var access = Generator(minutes: 30).Generate(UserWith(UserRole.Customer));

        Assert.Equal(Now.AddMinutes(30), access.ExpiresAtUtc);
        Assert.Equal(Now.AddMinutes(30), Read(access).ValidTo);
    }

    [Fact]
    public void Jwt_IsSignedHs256_AndOnlyValidatesWithTheSameKey()
    {
        var access = Generator().Generate(UserWith(UserRole.Customer));
        var handler = new JsonWebTokenHandler();

        TokenValidationParameters Parameters(string key) => new()
        {
            ValidIssuer = "OrderFlow",
            ValidAudience = "OrderFlow.Api",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ValidateLifetime = false,
        };

        Assert.Equal("HS256", Read(access).Alg);
        Assert.True(handler.ValidateTokenAsync(access.Token, Parameters(Key)).Result.IsValid);
        Assert.False(handler.ValidateTokenAsync(access.Token, Parameters("a-completely-different-key-also-32-bytes!!")).Result.IsValid);
    }

    [Fact]
    public void Jwt_EveryTokenHasAUniqueId()
    {
        var generator = Generator();
        var user = UserWith(UserRole.Customer);

        Assert.NotEqual(Read(generator.Generate(user)).Id, Read(generator.Generate(user)).Id);
    }

    [Fact]
    public void JwtOptions_ShortOrMissingKey_FailsValidation()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtOptions { SigningKey = "" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new JwtOptions { SigningKey = "too-short" }.Validate());
        new JwtOptions { SigningKey = Key }.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1441)]
    public void JwtOptions_OutOfRangeLifetime_FailsValidation(int minutes)
    {
        Assert.Throws<InvalidOperationException>(() => new JwtOptions { SigningKey = Key, ExpiryMinutes = minutes }.Validate());
    }
}
