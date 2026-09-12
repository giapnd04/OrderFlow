using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.GetOrderById;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Orders;

public class GetOrderByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingOrder_ReturnsOrderWithItems()
    {
        var order = Order.Create(1, new[]
        {
            OrderItem.Create(10, 2, 100m),
            OrderItem.Create(20, 1, 50m),
        });

        var handler = new GetOrderByIdQueryHandler(new FakeOrderRepository(order));

        var result = await handler.Handle(new GetOrderByIdQuery(order.Id));

        Assert.Equal(order.Id, result.OrderId);
        Assert.Equal(1, result.CustomerId);
        Assert.Equal(250m, result.TotalAmount);
        Assert.Collection(
            result.Items.OrderBy(i => i.ProductId),
            line => { Assert.Equal(10, line.ProductId); Assert.Equal(2, line.Quantity); Assert.Equal(100m, line.UnitPrice); },
            line => { Assert.Equal(20, line.ProductId); Assert.Equal(1, line.Quantity); Assert.Equal(50m, line.UnitPrice); });
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new GetOrderByIdQueryHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetOrderByIdQuery(999)));
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly Order? _order;

        public FakeOrderRepository(Order? order) => _order = order;

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetOrderById tests.");

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order is not null && _order.Id == orderId ? _order : null);
    }
}
