using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using OrderFlow.Domain.Enums;

namespace OrderFlow.IntegrationTests.Security;

public sealed class ApiSecurityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiSecurityTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? token = null, object? body = null)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(method, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await client.SendAsync(request);
    }

    private sealed class PastTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddHours(-3);
    }

    public static TheoryData<string, string> ProtectedEndpoints => new()
    {
        { "GET", "/api/orders" },
        { "GET", "/api/orders/1" },
        { "POST", "/api/orders" },
        { "GET", "/api/orders/summary" },
        { "POST", "/api/orders/1/ship" },
        { "GET", "/api/products" },
        { "POST", "/api/products" },
        { "GET", "/api/customers" },
        { "DELETE", "/api/customers/1" },
        { "GET", "/api/payments" },
        { "POST", "/api/users/staff" },
    };

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task NoToken_Returns401(string method, string path)
    {
        var response = await SendAsync(new HttpMethod(method), path);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unauthorized_IsAProblemDetailsBody()
    {
        var response = await SendAsync(HttpMethod.Get, "/api/orders");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task HandledErrors_AreProblemJson_NotPlainJson()
    {
        // Passes auth, then fails validation -> served by the exception handler, not the status-code pages.
        var token = TestTokens.For(UserRole.Administrator);

        var response = await SendAsync(
            HttpMethod.Post, "/api/users/staff", token,
            new { email = "s@example.com", password = "Password1", role = "Customer" });

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GarbageToken_Returns401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/orders", "not.a.jwt")).StatusCode);
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_Returns401()
    {
        var forged = TestTokens.For(UserRole.Administrator, signingKey: "an-attackers-different-key-of-sufficient-length!!");

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/orders", forged)).StatusCode);
    }

    [Fact]
    public async Task ExpiredToken_Returns401()
    {
        var expired = TestTokens.For(UserRole.Administrator, clock: new PastTimeProvider());

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/orders", expired)).StatusCode);
    }

    [Fact]
    public async Task UnsignedAlgNoneToken_Returns401()
    {
        // header {"alg":"none","typ":"JWT"}, payload {"sub":"1","role":"Administrator"}, empty signature
        const string unsigned = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiIxIiwicm9sZSI6IkFkbWluaXN0cmF0b3IifQ.";

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, "/api/users/staff", unsigned)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Delete, "/api/customers/1", unsigned)).StatusCode);
    }

    [Fact]
    public async Task TamperedPayload_Returns401()
    {
        var token = TestTokens.For(UserRole.Customer, customerId: 1);
        var parts = token.Split('.');
        // Swap in a payload claiming Administrator while keeping the original signature.
        var payload = Convert.ToBase64String("""{"sub":"1","role":"Administrator"}"""u8.ToArray()).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var tampered = $"{parts[0]}.{payload}.{parts[2]}";

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Delete, "/api/customers/1", tampered)).StatusCode);
    }

    public static TheoryData<UserRole, string, string> ForbiddenCombinations => new()
    {
        { UserRole.Customer, "DELETE", "/api/customers/1" },
        { UserRole.Customer, "POST", "/api/products" },
        { UserRole.Customer, "GET", "/api/customers" },
        { UserRole.Customer, "GET", "/api/payments" },
        { UserRole.Customer, "GET", "/api/orders/summary" },
        { UserRole.Customer, "POST", "/api/orders/1/ship" },
        { UserRole.Customer, "POST", "/api/orders/1/confirm" },
        { UserRole.Customer, "POST", "/api/products/1/restock" },
        { UserRole.Customer, "POST", "/api/users/staff" },
        { UserRole.Sales, "DELETE", "/api/customers/1" },
        { UserRole.Sales, "POST", "/api/orders/1/ship" },
        { UserRole.Sales, "POST", "/api/orders/1/deliver" },
        { UserRole.Sales, "POST", "/api/products/1/restock" },
        { UserRole.Sales, "POST", "/api/users/staff" },
        { UserRole.Warehouse, "POST", "/api/orders/1/confirm" },
        { UserRole.Warehouse, "POST", "/api/orders/1/payments" },
        { UserRole.Warehouse, "POST", "/api/customers" },
        { UserRole.Warehouse, "POST", "/api/products" },
        { UserRole.Warehouse, "GET", "/api/payments" },
        { UserRole.Warehouse, "DELETE", "/api/customers/1" },
        { UserRole.Warehouse, "POST", "/api/users/staff" },
    };

    [Theory]
    [MemberData(nameof(ForbiddenCombinations))]
    public async Task WrongRole_Returns403(UserRole role, string method, string path)
    {
        var token = TestTokens.For(role, customerId: role == UserRole.Customer ? 1 : null);

        var response = await SendAsync(new HttpMethod(method), path, token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UnlinkedCustomer_CannotListOrders_AndGetsAnExplanation()
    {
        var token = TestTokens.For(UserRole.Customer); // no customer_id claim: claim still pending

        var response = await SendAsync(HttpMethod.Get, "/api/orders", token);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("not linked to a customer record", body);
    }

    [Fact]
    public async Task Customer_CannotCreateAnOrderForSomeoneElse()
    {
        var token = TestTokens.For(UserRole.Customer, customerId: 1);

        var response = await SendAsync(
            HttpMethod.Post, "/api/orders", token,
            new { customerId = 2, items = new[] { new { productId = 1, quantity = 1 } } });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("your own customer record", body);
    }

    [Fact]
    public async Task Health_Live_IsAnonymousAndUp()
    {
        var response = await SendAsync(HttpMethod.Get, "/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyBody_ReturnsAllValidationErrorsWithoutTouchingTheDatabase()
    {
        var response = await SendAsync(HttpMethod.Post, "/api/auth/login", body: new { email = "", password = "" });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task Register_WithWeakPasswordAndBadEmail_ListsEachProblem()
    {
        var response = await SendAsync(
            HttpMethod.Post, "/api/auth/register",
            body: new { name = "A", email = "nope", password = "abc", phone = (string?)null });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = json.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Email", out _));
        Assert.True(errors.TryGetProperty("Password", out _));
    }

    [Fact]
    public async Task EnumsInRequestBodies_AcceptNames()
    {
        // Admin passes auth, then the validator rejects Role=Customer: proves "Customer" was parsed by name.
        var token = TestTokens.For(UserRole.Administrator);

        var response = await SendAsync(
            HttpMethod.Post, "/api/users/staff", token,
            new { email = "s@example.com", password = "Password1", role = "Customer" });
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty("Role", out _));
    }
}

public sealed class RateLimitFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>
    {
        ["RateLimiting:Auth:PermitLimit"] = "3",
        ["RateLimiting:WindowSeconds"] = "60",
    };
}

public sealed class RateLimitTests : IClassFixture<RateLimitFactory>
{
    private readonly RateLimitFactory _factory;

    public RateLimitTests(RateLimitFactory factory) => _factory = factory;

    [Fact]
    public async Task AuthEndpoints_AreThrottledPerClient_ButOtherEndpointsAreNot()
    {
        using var client = _factory.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            var allowed = await client.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });
            Assert.Equal(HttpStatusCode.BadRequest, allowed.StatusCode);
        }

        var limited = await client.PostAsJsonAsync("/api/auth/login", new { email = "", password = "" });

        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
        Assert.Equal("application/problem+json", limited.Content.Headers.ContentType?.MediaType);

        var health = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }
}

public sealed class CorsFactory : ApiFactory
{
    protected override IReadOnlyDictionary<string, string> ExtraSettings { get; } = new Dictionary<string, string>
    {
        ["Cors:AllowedOrigins:0"] = "https://app.example.com",
    };
}

public sealed class CorsTests : IClassFixture<CorsFactory>, IClassFixture<ApiFactory>
{
    private readonly CorsFactory _withCors;
    private readonly ApiFactory _withoutCors;

    public CorsTests(CorsFactory withCors, ApiFactory withoutCors)
    {
        _withCors = withCors;
        _withoutCors = withoutCors;
    }

    private static async Task<string?> AllowOriginAsync(ApiFactory factory, string origin)
    {
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth/login");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        return response.Headers.TryGetValues("Access-Control-Allow-Origin", out var values) ? values.Single() : null;
    }

    [Fact]
    public async Task ConfiguredOrigin_IsAllowed() =>
        Assert.Equal("https://app.example.com", await AllowOriginAsync(_withCors, "https://app.example.com"));

    [Fact]
    public async Task UnlistedOrigin_IsNotAllowed() =>
        Assert.Null(await AllowOriginAsync(_withCors, "https://evil.example.com"));

    [Fact]
    public async Task WithNothingConfigured_NoOriginIsAllowed_AndThereIsNoWildcard() =>
        Assert.Null(await AllowOriginAsync(_withoutCors, "https://app.example.com"));
}
