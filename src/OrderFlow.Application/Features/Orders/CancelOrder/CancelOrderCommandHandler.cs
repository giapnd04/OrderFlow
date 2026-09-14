using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Orders.CancelOrder;

public sealed class CancelOrderCommandHandler : ICommandHandler<CancelOrderCommand, CancelOrderResult>
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;

    public CancelOrderCommandHandler(IOrderRepository orders, IProductRepository products)
    {
        _orders = orders;
        _products = products;
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

        var previousStatus = order.Status;

        order.Cancel();

        var productIds = order.Items
            .Select(item => item.ProductId)
            .Distinct()
            .ToList();

        var products = await _products.GetByIdsAsync(
            productIds,
            cancellationToken);

        var productsById = products.ToDictionary(product => product.Id);

        foreach (var item in order.Items)
        {
            if (!productsById.TryGetValue(item.ProductId, out var product))
            {
                throw new NotFoundException("Product", item.ProductId);
            }

            if (previousStatus == OrderStatus.PendingPayment)
            {
                product.ReleaseReservation(item.Quantity);
            }
            else if (previousStatus == OrderStatus.Confirmed)
            {
                product.Restock(item.Quantity);
            }
        }

        await _orders.UpdateAsync(order, cancellationToken);

        return new CancelOrderResult(order.Id, order.Status.ToString());
    }
}
