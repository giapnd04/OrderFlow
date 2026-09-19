using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.DeliverOrder;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Orders;

public class DeliverOrderCommandHandlerTests
{
    private static Order ShippedOrder()
    {
        var order = Order.Create(1, new[] { OrderItem.Create(10, 1, 100m) });
        order.Id = 42;
        order.MarkAsPaid();
        order.Confirm();
        order.Ship();
        return order;
    }

    [Fact]
    public async Task Handle_ShippedOrder_TransitionsToDelivered()
    {
        var order = ShippedOrder();
        var handler = new DeliverOrderCommandHandler(new FakeOrderRepository(order));

        var result = await handler.Handle(new DeliverOrderCommand(order.Id));

        Assert.Equal(nameof(OrderStatus.Delivered), result.Status);
        Assert.Equal(OrderStatus.Delivered, order.Status);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new DeliverOrderCommandHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeliverOrderCommand(999)));
    }

    [Fact]
    public async Task Handle_OrderNotShipped_ThrowsInvalidOrderState()
    {
        var order = Order.Create(1, new[] { OrderItem.Create(10, 1, 100m) });
        order.Id = 42;
        var handler = new DeliverOrderCommandHandler(new FakeOrderRepository(order));

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => handler.Handle(new DeliverOrderCommand(order.Id)));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = new DeliverOrderCommandHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new DeliverOrderCommand(0)));
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly Order? _order;

        public FakeOrderRepository(Order? order) => _order = order;

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for DeliverOrder tests.");

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for DeliverOrder tests.");

        public Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order is not null && _order.Id == orderId ? _order : null);

        public Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(OrderStatus? status, int? customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for DeliverOrder tests.");

        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
