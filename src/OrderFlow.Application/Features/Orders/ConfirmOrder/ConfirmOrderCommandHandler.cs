using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.ConfirmOrder;

public sealed class ConfirmOrderCommandHandler : ICommandHandler<ConfirmOrderCommand, ConfirmOrderResult>
{
    private readonly IOrderRepository _orders;

    public ConfirmOrderCommandHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<ConfirmOrderResult> Handle(ConfirmOrderCommand command, CancellationToken cancellationToken = default)
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

        order.Confirm();

        await _orders.UpdateAsync(order, cancellationToken);

        return new ConfirmOrderResult(order.Id, order.Status.ToString());
    }
}
