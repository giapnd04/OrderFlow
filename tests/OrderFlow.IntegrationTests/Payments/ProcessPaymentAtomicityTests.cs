using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace OrderFlow.IntegrationTests.Payments;

// OF-03 / G-01: ProcessPayment writes a payment and then the order + products. If the second
// write fails, neither may survive.
[Collection(SqlServerCollection.Name)]
public sealed class ProcessPaymentAtomicityTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private readonly ITestOutputHelper _output;

    public ProcessPaymentAtomicityTests(SqlServerFixture sql, ITestOutputHelper output)
    {
        _sql = sql;
        _output = output;
    }

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ProcessPayment_HappyPath_PaysOrderAndFulfillsStock()
    {
        var (orderId, productId) = await TestData.SeedPendingOrderAsync(_sql);

        await PayInFullAsync(orderId);

        await using var db = _sql.CreateDbContext();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(1, await db.PaymentAttempts.CountAsync());
        Assert.Equal(TestData.StockQuantity - TestData.OrderedQuantity, product.StockQuantity);
        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public async Task ProcessPayment_SecondSaveFails_LeavesNoSucceededPaymentOnPendingOrder()
    {
        var (orderId, productId) = await TestData.SeedPendingOrderAsync(_sql);

        await Assert.ThrowsAsync<InjectedFaultException>(
            () => PayInFullAsync(orderId, new FailOnSaveChangesInterceptor(failOnCall: 2)));

        await using var db = _sql.CreateDbContext();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        var product = await db.Products.SingleAsync(p => p.Id == productId);
        var payments = await db.PaymentAttempts.CountAsync(p => p.OrderId == orderId);

        Assert.True(payments == 0,
            $"Expected the payment to roll back with the failed order update, but found {payments} " +
            $"payment row(s) while the order is still {order.Status}.");
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(TestData.StockQuantity, product.StockQuantity);
        Assert.Equal(TestData.OrderedQuantity, product.ReservedQuantity);
    }

    private async Task PayInFullAsync(int orderId, params IInterceptor[] faults)
    {
        await using var services = _sql.CreateServiceProvider(_output, faults);
        await using var scope = services.CreateAsyncScope();

        var handler = scope.ServiceProvider.GetRequiredService<ProcessPaymentCommandHandler>();

        await handler.Handle(
            new ProcessPaymentCommand(orderId, TestData.OrderTotal, "TestPay", "TX-1", PaymentAttemptStatus.Succeeded),
            CancellationToken.None);
    }
}
