using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.GetOrderById;

public sealed class GetOrderByIdQueryHandler
    : IQueryHandler<GetOrderByIdQuery, GetOrderByIdResult>
{
    private readonly IOrderRepository _orders;

    public GetOrderByIdQueryHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<GetOrderByIdResult> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var order = await _orders.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", query.OrderId);
        }

        return new GetOrderByIdResult(
            order.Id,
            order.CustomerId,
            order.Status.ToString(),
            order.TotalAmount,
            order.Items
                .Select(item => new GetOrderByIdItemResult(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice,
                    item.Subtotal))
                .ToList());
    }
}
