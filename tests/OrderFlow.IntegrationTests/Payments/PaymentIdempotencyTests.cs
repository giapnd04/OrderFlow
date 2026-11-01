using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Payments;

// OF-05 / G-02: a payment callback may arrive twice, or two different payments may race for
// the same order. Money must be recorded once and never beyond the order total.
[Collection(SqlServerCollection.Name)]
public sealed class PaymentIdempotencyTests : IAsyncLifetime
{
    private const int CheckConstraintViolation = 547;

    private readonly SqlServerFixture _sql;
    private SqlApiFactory? _factory;
    private HttpClient? _client;

    public PaymentIdempotencyTests(SqlServerFixture sql) => _sql = sql;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();

        _factory = new SqlApiFactory(_sql.ConnectionString);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Sales));
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();

        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    [Fact]
    public async Task SamePayment_SentFiveTimes_RecordsOneRowAndReplaysTheResult()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);

        var responses = new List<(HttpStatusCode Status, string Body)>();
        for (var i = 0; i < 5; i++)
        {
            responses.Add(await PayAsync(orderId, TestData.OrderTotal, "TX-REPLAY"));
        }

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.Status));
        Assert.Single(responses.Select(r => PaymentAttemptId(r.Body)).Distinct());

        await using var db = _sql.CreateDbContext();
        Assert.Equal(1, await db.PaymentAttempts.CountAsync());
        Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task SameTransactionId_DifferentAmount_IsRejectedWithoutChangingData()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);
        Assert.Equal(HttpStatusCode.OK, (await PayAsync(orderId, 5m, "TX-PAYLOAD")).Status);

        var second = await PayAsync(orderId, 15m, "TX-PAYLOAD");

        Assert.Equal(HttpStatusCode.Conflict, second.Status);

        await using var db = _sql.CreateDbContext();
        Assert.Equal(5m, (await db.PaymentAttempts.SingleAsync()).Amount);
        Assert.Equal(OrderStatus.PendingPayment, (await db.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task SucceededPayment_WithoutTransactionId_IsRejected()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);

        var response = await PayAsync(orderId, TestData.OrderTotal, providerTransactionId: null);

        Assert.Equal(HttpStatusCode.BadRequest, response.Status);

        await using var db = _sql.CreateDbContext();
        Assert.Equal(0, await db.PaymentAttempts.CountAsync());
    }

    [Fact]
    public async Task TwoFullPayments_DifferentIds_Concurrent_PayTheOrderOnce()
    {
        var (orderId, productId) = await TestData.SeedPendingOrderAsync(_sql);

        var results = await RaceAsync(
            () => PayAsync(orderId, TestData.OrderTotal, "TX-A"),
            () => PayAsync(orderId, TestData.OrderTotal, "TX-B"));

        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Conflict));

        await using var db = _sql.CreateDbContext();
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.Equal(1, await db.PaymentAttempts.CountAsync(p => p.Status == PaymentAttemptStatus.Succeeded));
        Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync()).Status);
        Assert.Equal(TestData.StockQuantity - TestData.OrderedQuantity, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public async Task TwoPartialPayments_Concurrent_NeverExceedTheOrderTotal()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);

        var results = await RaceAsync(
            () => PayAsync(orderId, 15m, "TX-P1"),
            () => PayAsync(orderId, 15m, "TX-P2"));

        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.OK));
        Assert.Equal(1, results.Count(r => r.Status == HttpStatusCode.Conflict));

        await using var db = _sql.CreateDbContext();
        var paid = await db.PaymentAttempts
            .Where(p => p.Status == PaymentAttemptStatus.Succeeded)
            .SumAsync(p => p.Amount);

        Assert.Equal(15m, paid);
        Assert.Equal(OrderStatus.PendingPayment, (await db.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task SamePayment_Concurrent_BothSucceedWithOneRow()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);

        var results = await RaceAsync(
            () => PayAsync(orderId, TestData.OrderTotal, "TX-TWIN"),
            () => PayAsync(orderId, TestData.OrderTotal, "TX-TWIN"));

        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.Status));

        await using var db = _sql.CreateDbContext();
        Assert.Equal(1, await db.PaymentAttempts.CountAsync());
    }

    [Fact]
    public async Task Insert_SucceededWithoutTransactionId_IsRejectedByCheckConstraint()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);
        await using var db = _sql.CreateDbContext();

        var ex = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payment_attempts (order_id, amount, provider, provider_transaction_id, status)
            VALUES ({orderId}, {TestData.OrderTotal}, {"TestPay"}, {null}, {"Succeeded"})
            """));

        Assert.Equal(CheckConstraintViolation, ex.Number);
        Assert.Contains("ck_payment_attempts_succeeded_requires_provider_tx", ex.Message);
    }

    private async Task<(HttpStatusCode Status, string Body)> PayAsync(int orderId, decimal amount, string? providerTransactionId)
    {
        using var response = await Client.PostAsJsonAsync($"/api/orders/{orderId}/payments", new
        {
            amount,
            provider = "TestPay",
            providerTransactionId,
            status = "Succeeded",
        });

        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(HttpStatusCode Status, string Body)[]> RaceAsync(
        params Func<Task<(HttpStatusCode Status, string Body)>>[] calls)
    {
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = calls.Select(async call =>
        {
            await start.Task;
            return await call();
        }).ToList();

        start.SetResult();
        return await Task.WhenAll(tasks);
    }

    private static int PaymentAttemptId(string body)
        => JsonDocument.Parse(body).RootElement.GetProperty("paymentAttemptId").GetInt32();
}
