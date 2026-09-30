using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.ConfirmOrder;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Orders;

public sealed class ConfirmOrderCommandHandlerTests
{
    private static Order NewOrder()
    {
        var order = Order.Create(1, new[] { OrderItem.Create(10, 1, 100m) });
        order.Id = 42;
        return order;
    }

    [Fact]
    public async Task Handle_PaidOrder_TransitionsToConfirmed()
    {
        var order = NewOrder();
        order.MarkAsPaid();
        var handler = new ConfirmOrderCommandHandler(new FakeOrderRepository(order));

        var result = await handler.Handle(new ConfirmOrderCommand(order.Id));

        Assert.Equal(nameof(OrderStatus.Confirmed), result.Status);
        Assert.Equal(OrderStatus.Confirmed, order.Status);
    }

    [Fact]
    public async Task Handle_PendingPaymentOrder_ThrowsInvalidOrderState()
    {
        var order = NewOrder();
        var handler = new ConfirmOrderCommandHandler(new FakeOrderRepository(order));

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => handler.Handle(new ConfirmOrderCommand(order.Id)));
    }

    [Fact]
    public async Task Handle_AlreadyConfirmedOrder_ThrowsInvalidOrderState()
    {
        var order = NewOrder();
        order.MarkAsPaid();
        order.Confirm();
        var handler = new ConfirmOrderCommandHandler(new FakeOrderRepository(order));

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => handler.Handle(new ConfirmOrderCommand(order.Id)));
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new ConfirmOrderCommandHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ConfirmOrderCommand(999)));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = new ConfirmOrderCommandHandler(new FakeOrderRepository(order: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ConfirmOrderCommand(0)));
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly Order? _order;

        public FakeOrderRepository(Order? order) => _order = order;

        public Task AddAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order is not null && _order.Id == orderId ? _order : null);

        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(
            OrderStatus? status, int? customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
