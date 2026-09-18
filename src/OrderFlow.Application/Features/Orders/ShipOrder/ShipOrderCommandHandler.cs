using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.ShipOrder;

public sealed class ShipOrderCommandHandler : ICommandHandler<ShipOrderCommand, ShipOrderResult>
{
    private readonly IOrderRepository _orders;

    public ShipOrderCommandHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<ShipOrderResult> Handle(ShipOrderCommand command, CancellationToken cancellationToken = default)
    {
        if (command.OrderId <= 0)
        {
            throw new ValidationException("OrderId must be greater than zero.");
        }

        var order = await _orders.GetByIdForUpdateAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", command.OrderId);
        }

        order.Ship();

        await _orders.UpdateAsync(order, cancellationToken);

        return new ShipOrderResult(order.Id, order.Status.ToString());
    }
}
