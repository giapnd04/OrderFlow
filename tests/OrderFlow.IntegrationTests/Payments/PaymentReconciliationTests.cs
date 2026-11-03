using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Features.Payments.ReconcilePayments;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using OrderFlow.IntegrationTests.Security;

namespace OrderFlow.IntegrationTests.Payments;

// OF-06 / INC-001: the provider collected the money but the order is still PendingPayment.
// Reconciliation must repair what it safely can, report the rest, and be safe to run again.
[Collection(SqlServerCollection.Name)]
public sealed class PaymentReconciliationTests : IAsyncLifetime
{
    private const string ReconcileUrl = "/api/admin/reconciliation/payments";
    private const string Provider = "TestPay";

    private readonly SqlServerFixture _sql;
    private SqlApiFactory? _factory;
    private HttpClient? _client;

    public PaymentReconciliationTests(SqlServerFixture sql) => _sql = sql;

    private HttpClient Client => _client ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        await _sql.ResetDatabaseAsync();

        _factory = new SqlApiFactory(_sql.ConnectionString);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Administrator));
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
    public async Task ProviderCharged_NoPaymentRecorded_RepairsOrder_AndSecondRunChangesNothing()
    {
        var (orderId, productId) = await TestData.SeedPendingOrderAsync(_sql);
        await SeedProviderChargeAsync("TX-LOST", orderId, TestData.OrderTotal);

        var first = await ReconcileAsync();

        Assert.Equal(1, first.Checked);
        Assert.Equal(1, first.PaymentsRecorded);
        Assert.Equal(1, first.OrdersCompleted);
        Assert.Equal(0, first.Unchanged);
        Assert.Empty(first.NeedsReview);
        await AssertPaidOnceAsync(orderId, productId);

        var second = await ReconcileAsync();

        Assert.Equal(1, second.Checked);
        Assert.Equal(0, second.PaymentsRecorded);
        Assert.Equal(0, second.OrdersCompleted);
        Assert.Equal(1, second.Unchanged);
        Assert.Empty(second.NeedsReview);
        await AssertPaidOnceAsync(orderId, productId);
    }

    [Fact]
    public async Task PaymentRecorded_OrderStuckPending_CompletesOrderWithoutSecondPayment()
    {
        // The state the double commit used to leave behind (G-01): money recorded, order not updated.
        var (orderId, productId) = await TestData.SeedPendingOrderAsync(_sql);
        await SeedProviderChargeAsync("TX-STUCK", orderId, TestData.OrderTotal);
        await InsertSucceededPaymentAsync("TX-STUCK", orderId, TestData.OrderTotal);

        var result = await ReconcileAsync();

        Assert.Equal(0, result.PaymentsRecorded);
        Assert.Equal(1, result.OrdersCompleted);
        await AssertPaidOnceAsync(orderId, productId);
    }

    [Fact]
    public async Task ProviderChargedCancelledOrder_IsReported_AndHealthyChargeStillProcessed()
    {
        var (cancelledOrderId, _) = await TestData.SeedPendingOrderAsync(_sql);
        await SetOrderStatusAsync(cancelledOrderId, "Cancelled");
        await SeedProviderChargeAsync("TX-CANCELLED", cancelledOrderId, TestData.OrderTotal);

        var healthyOrderId = await SeedSecondPendingOrderAsync();
        await SeedProviderChargeAsync("TX-HEALTHY", healthyOrderId, TestData.OrderTotal);

        var result = await ReconcileAsync();

        var issue = Assert.Single(result.NeedsReview);
        Assert.Equal("TX-CANCELLED", issue.ProviderTransactionId);
        Assert.Equal(cancelledOrderId, issue.OrderId);
        Assert.Equal(1, result.PaymentsRecorded);
        Assert.Equal(1, result.OrdersCompleted);

        await using var db = _sql.CreateDbContext();
        Assert.Equal(0, await db.PaymentAttempts.CountAsync(p => p.OrderId == cancelledOrderId));
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.SingleAsync(o => o.Id == cancelledOrderId)).Status);
        Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync(o => o.Id == healthyOrderId)).Status);
    }

    [Fact]
    public async Task ProviderAmountDiffersFromRecordedPayment_IsReportedAndNothingChanges()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);
        await InsertSucceededPaymentAsync("TX-MISMATCH", orderId, 5m);
        await SeedProviderChargeAsync("TX-MISMATCH", orderId, 15m);

        var result = await ReconcileAsync();

        Assert.Equal("TX-MISMATCH", Assert.Single(result.NeedsReview).ProviderTransactionId);

        await using var db = _sql.CreateDbContext();
        Assert.Equal(5m, (await db.PaymentAttempts.SingleAsync()).Amount);
        Assert.Equal(OrderStatus.PendingPayment, (await db.Orders.SingleAsync()).Status);
    }

    [Fact]
    public async Task ProviderChargeForUnknownOrder_IsReported()
    {
        await SeedProviderChargeAsync("TX-ORPHAN", orderId: 999999, TestData.OrderTotal);

        var result = await ReconcileAsync();

        Assert.Equal("TX-ORPHAN", Assert.Single(result.NeedsReview).ProviderTransactionId);
    }

    [Fact]
    public async Task EverythingAlreadyInSync_ReportsUnchanged()
    {
        var (orderId, _) = await TestData.SeedPendingOrderAsync(_sql);
        await SeedProviderChargeAsync("TX-OK", orderId, TestData.OrderTotal);
        await ReconcileAsync();

        var result = await ReconcileAsync();

        Assert.Equal(1, result.Unchanged);
        Assert.Equal(0, result.PaymentsRecorded + result.OrdersCompleted);
    }

    [Fact]
    public async Task Reconcile_RequiresAdministrator()
    {
        using var sales = new HttpRequestMessage(HttpMethod.Post, ReconcileUrl);
        sales.Headers.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(UserRole.Sales));
        using var salesResponse = await Client.SendAsync(sales);

        using var anonymousClient = _factory!.CreateClient();
        using var anonymousResponse = await anonymousClient.PostAsync(ReconcileUrl, content: null);

        Assert.Equal(HttpStatusCode.Forbidden, salesResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
    }

    private async Task<ReconcilePaymentsResult> ReconcileAsync()
    {
        using var response = await Client.PostAsync(ReconcileUrl, content: null);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ReconcilePaymentsResult>()
            ?? throw new InvalidOperationException("Empty reconciliation response.");
    }

    private async Task AssertPaidOnceAsync(int orderId, int productId)
    {
        await using var db = _sql.CreateDbContext();
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.Equal(1, await db.PaymentAttempts.CountAsync(p => p.OrderId == orderId));
        Assert.Equal(OrderStatus.Paid, (await db.Orders.SingleAsync(o => o.Id == orderId)).Status);
        Assert.Equal(TestData.StockQuantity - TestData.OrderedQuantity, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    private async Task SeedProviderChargeAsync(string transactionId, int orderId, decimal amount)
    {
        await using var db = _sql.CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO sim.provider_transactions (provider, provider_transaction_id, order_id, amount)
            VALUES ({Provider}, {transactionId}, {orderId}, {amount})
            """);
    }

    private async Task InsertSucceededPaymentAsync(string transactionId, int orderId, decimal amount)
    {
        await using var db = _sql.CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO payment_attempts (order_id, amount, provider, provider_transaction_id, status)
            VALUES ({orderId}, {amount}, {Provider}, {transactionId}, {"Succeeded"})
            """);
    }

    private async Task SetOrderStatusAsync(int orderId, string status)
    {
        await using var db = _sql.CreateDbContext();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE orders SET status = {status} WHERE id = {orderId}");
    }

    private async Task<int> SeedSecondPendingOrderAsync()
    {
        await using var db = _sql.CreateDbContext();
        var customer = await db.Customers.FirstAsync();
        var product = await db.Products.FirstAsync();

        product.Reserve(TestData.OrderedQuantity);

        var order = Order.Create(
            customer.Id,
            [OrderItem.Create(product.Id, TestData.OrderedQuantity, TestData.UnitPrice)]);

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return order.Id;
    }
}
