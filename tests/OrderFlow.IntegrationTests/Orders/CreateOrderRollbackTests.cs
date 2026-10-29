using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace OrderFlow.IntegrationTests.Orders;

// CreateOrder persists with a single SaveChanges, so EF's implicit transaction must undo the
// already-executed INSERT INTO orders when a later statement of the same save fails.
[Collection(SqlServerCollection.Name)]
public sealed class CreateOrderRollbackTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private readonly ITestOutputHelper _output;

    public CreateOrderRollbackTests(SqlServerFixture sql, ITestOutputHelper output)
    {
        _sql = sql;
        _output = output;
    }

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CreateOrder_OrderItemsInsertFails_RollsBackOrderAndReservation()
    {
        var (customerId, productId) = await TestData.SeedCustomerAndProductAsync(_sql);

        await using (var services = _sql.CreateServiceProvider(_output, new FailOnCommandInterceptor("INSERT INTO [order_items]")))
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CreateOrderCommandHandler>();

            // EF wraps a failure inside SaveChanges, so the injected fault arrives as the inner exception.
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => handler.Handle(
                new CreateOrderCommand(customerId, [new CreateOrderItemInput(productId, TestData.OrderedQuantity)]),
                CancellationToken.None));

            Assert.IsType<InjectedFaultException>(ex.InnerException);
        }

        await using var db = _sql.CreateDbContext();
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.Equal(0, await db.Orders.CountAsync());
        Assert.Equal(0, await db.OrderItems.CountAsync());
        Assert.Equal(0, product.ReservedQuantity);
    }
}
