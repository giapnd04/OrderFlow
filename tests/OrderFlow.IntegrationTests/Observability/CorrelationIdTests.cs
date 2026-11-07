using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using OrderFlow.API.Middleware;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Observability;

// OF-10: one id per request, returned to the caller and stamped on every log line, so a failed
// request can be traced end to end. None of these requests reaches the database.
public sealed class CorrelationIdTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public CorrelationIdTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task NoHeaderSent_ResponseCarriesANewId()
    {
        using var response = await SendAsync("/health/live");

        var id = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.True(Guid.TryParseExact(id, "N", out _), $"expected a generated GUID, got '{id}'");
    }

    [Fact]
    public async Task ValidHeaderSent_IsEchoedBack()
    {
        using var response = await SendAsync("/health/live", "checkout-7f3a.retry:2");

        Assert.Equal("checkout-7f3a.retry:2", Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("line\nbreak")]
    [InlineData("<script>")]
    public async Task UnsafeHeaderSent_IsReplaced(string unsafeId)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, unsafeId);

        using var response = await client.SendAsync(request);

        var id = Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.NotEqual(unsafeId, id);
        Assert.True(Guid.TryParseExact(id, "N", out _));
    }

    [Fact]
    public async Task TooLongHeader_IsReplaced()
    {
        using var response = await SendAsync("/health/live", new string('a', 65));

        Assert.Equal(32, Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)).Length);
    }

    [Theory]
    [InlineData("/api/auth/login", HttpStatusCode.BadRequest)]
    [InlineData("/api/orders", HttpStatusCode.Unauthorized)]
    public async Task ErrorBody_CarriesTheSameIdAsTheHeader(string path, HttpStatusCode expected)
    {
        using var client = _factory.CreateClient();
        using var request = path.Contains("login")
            ? new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) }
            : new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "trace-me-1");

        using var response = await client.SendAsync(request);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("trace-me-1", body.GetProperty("correlationId").GetString());
    }

    [Fact]
    public async Task EveryLogLineOfAFailingRequest_HasItsCorrelationId()
    {
        var logs = new CapturingLoggerProvider();
        await using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(logs)));
        using var client = factory.CreateClient();

        // Passes authentication, then the handler rejects top=0: an exception the global handler logs.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/reports/top-customers?top=0");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestTokens.For(OrderFlow.Domain.Enums.UserRole.Sales));
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "failing-request-42");
        using var response = await client.SendAsync(request);

        var requestLogs = logs.Logs
            .Where(log => log.Scope.TryGetValue("CorrelationId", out var id) && Equals(id, "failing-request-42"))
            .ToList();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(requestLogs, log => log.Category.EndsWith("GlobalExceptionHandler") && log.Level == LogLevel.Warning);

        // No line written while handling this request is missing the id.
        Assert.DoesNotContain(logs.Logs, log => log.Message.Contains("top-customers") && !log.Scope.ContainsKey("CorrelationId"));
    }

    [Fact]
    public async Task HealthReady_Returns503WhenTheDatabaseIsUnreachable()
    {
        // ApiFactory points at a connection string that cannot connect.
        using var response = await SendAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private async Task<HttpResponseMessage> SendAsync(string path, string? correlationId = null)
    {
        using var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (correlationId is not null)
        {
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return await client.SendAsync(request);
    }
}
