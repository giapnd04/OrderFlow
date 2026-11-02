using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Domain.Enums;
using OrderFlow.IntegrationTests.Fixtures;
using Xunit.Abstractions;

namespace OrderFlow.IntegrationTests.Orders;

// G-05: CancelOrder saves the order and the released reservation in one SaveChanges. EF sends
// both UPDATEs in one batch, so failing a command would stop the whole batch before it runs and
// prove nothing. A real rowversion conflict on the product fails the batch half-way instead.
[Collection(SqlServerCollection.Name)]
public sealed class CancelOrderRollbackTests : IAsyncLifetime
{
    private readonly SqlServerFixture _sql;
    private readonly ITestOutputHelper _output;

    public CancelOrderRollbackTests(SqlServerFixture sql, ITestOutputHelper output)
    {
        _sql = sql;
        _output = output;
    }

    public Task InitializeAsync() => _sql.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task CancelOrder_ProductChangedBeforeSave_RollsBackOrderAndReservation()
    {
        var (orderId, productId) = await TestData.SeedPendingOrderAsync(_sql);

        var anotherSession = new BeforeSaveChangesInterceptor(async () =>
        {
            await using var other = _sql.CreateDbContext();
            await other.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE products SET updated_at = SYSUTCDATETIME() WHERE id = {productId}");
        });

        await using (var services = _sql.CreateServiceProvider(_output, anotherSession))
        await using (var scope = services.CreateAsyncScope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<CancelOrderCommandHandler>();

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => handler.Handle(new CancelOrderCommand(orderId), CancellationToken.None));
        }

        await using var db = _sql.CreateDbContext();
        var order = await db.Orders.SingleAsync(o => o.Id == orderId);
        var product = await db.Products.SingleAsync(p => p.Id == productId);

        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(TestData.OrderedQuantity, product.ReservedQuantity);
    }
}
