using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using OrderFlow.API.Middleware;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Observability;

// OF-10: business events are logged with their own EventName and the correlation id of the
// request that caused them, so "what happened to order 12" can be answered from the logs.
[Collection(SqlServerCollection.Name)]
public sealed class BusinessEventLogTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;

    public BusinessEventLogTests(SqlServerFixture sql) => _sql = sql;

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateAndPay_LogsEachEventUnderTheRequestThatCausedIt()
    {
        var (customerId, productId) = await TestData.SeedCustomerAndProductAsync(_sql);
        var logs = new CapturingLoggerProvider();

        await using var baseFactory = new SqlApiFactory(_sql.ConnectionString);
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureLogging(logging => logging.AddProvider(logs)));
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Sales));

        using var create = new HttpRequestMessage(HttpMethod.Post, "/api/orders")
        {
            Content = JsonContent.Create(new { customerId, items = new[] { new { productId, quantity = 2 } } }),
        };
        create.Headers.Add(CorrelationIdMiddleware.HeaderName, "create-order-1");
        using var created = await client.SendAsync(create);
        created.EnsureSuccessStatusCode();

        var orderId = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement.GetProperty("orderId").GetInt32();

        using var pay = new HttpRequestMessage(HttpMethod.Post, $"/api/orders/{orderId}/payments")
        {
            Content = JsonContent.Create(new
            {
                amount = TestData.UnitPrice * 2,
                provider = "TestPay",
                providerTransactionId = "TX-LOG-1",
                status = "Succeeded",
            }),
        };
        pay.Headers.Add(CorrelationIdMiddleware.HeaderName, "pay-order-1");
        using var paid = await client.SendAsync(pay);
        paid.EnsureSuccessStatusCode();

        AssertEvent(logs, "OrderCreated", "create-order-1", $"Order {orderId} created");
        AssertEvent(logs, "PaymentRecorded", "pay-order-1", $"for order {orderId}");
        AssertEvent(logs, "OrderPaid", "pay-order-1", $"Order {orderId} fully paid");
    }

    private static void AssertEvent(CapturingLoggerProvider logs, string eventName, string correlationId, string text)
    {
        var entry = Assert.Single(logs.Logs, log => log.EventName == eventName);

        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal(correlationId, entry.Scope["CorrelationId"]);
        Assert.Contains(text, entry.Message);
    }
}
