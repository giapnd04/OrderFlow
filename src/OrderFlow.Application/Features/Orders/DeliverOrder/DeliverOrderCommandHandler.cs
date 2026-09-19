using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.DeliverOrder;

public sealed class DeliverOrderCommandHandler : ICommandHandler<DeliverOrderCommand, DeliverOrderResult>
{
    private readonly IOrderRepository _orders;

    public DeliverOrderCommandHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<DeliverOrderResult> Handle(DeliverOrderCommand command, CancellationToken cancellationToken = default)
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

        order.Deliver();

        await _orders.UpdateAsync(order, cancellationToken);

        return new DeliverOrderResult(order.Id, order.Status.ToString());
    }
}
