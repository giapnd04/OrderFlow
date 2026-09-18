using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.ShipOrder;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Orders;

public class ShipOrderCommandHandlerTests
{
    private static Order ConfirmedOrder()
    {
        var order = Order.Create(1, new[] { OrderItem.Create(10, 1, 100m) });
        order.Id = 42;
        order.MarkAsPaid();
        order.Confirm();
        return order;
    }

    [Fact]
    public async Task Handle_ConfirmedOrder_TransitionsToShipped()
    {
        var order = ConfirmedOrder();
        var handler = new ShipOrderCommandHandler(new FakeOrderRepository(order));

        var result = await handler.Handle(new ShipOrderCommand(order.Id));

        Assert.Equal(nameof(OrderStatus.Shipped), result.Status);
        Assert.Equal(OrderStatus.Shipped, order.Status);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new ShipOrderCommandHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ShipOrderCommand(999)));
    }

    [Fact]
    public async Task Handle_OrderNotConfirmed_ThrowsInvalidOrderState()
    {
        var order = Order.Create(1, new[] { OrderItem.Create(10, 1, 100m) });
        order.Id = 42;
        var handler = new ShipOrderCommandHandler(new FakeOrderRepository(order));

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => handler.Handle(new ShipOrderCommand(order.Id)));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = new ShipOrderCommandHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ShipOrderCommand(0)));
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly Order? _order;

        public FakeOrderRepository(Order? order) => _order = order;

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ShipOrder tests.");

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ShipOrder tests.");

        public Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order is not null && _order.Id == orderId ? _order : null);

        public Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(OrderStatus? status, int? customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ShipOrder tests.");

        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
