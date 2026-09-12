using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.CancelOrder;

public sealed class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, CancelOrderResult>
{
    private readonly IOrderRepository _orders;

    public CancelOrderCommandHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<CancelOrderResult> Handle(CancelOrderCommand command, CancellationToken cancellationToken = default)
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

        order.Cancel();

        await _orders.UpdateAsync(order, cancellationToken);

        return new CancelOrderResult(order.Id, order.Status.ToString());
    }
}
