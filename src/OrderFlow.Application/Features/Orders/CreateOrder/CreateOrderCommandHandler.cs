using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Orders.CreateOrder;

/// <summary>
/// Coordinates the CreateOrder use case (SDS §5, §10):
/// validate the request, confirm the customer exists, load and validate the ordered
/// products, snapshot their prices onto new order lines, let the <see cref="Order"/>
/// aggregate build itself (and compute its total), then persist it.
///
/// Out of scope for this slice: stock check / reservation (schema v1 decrements stock
/// on payment success and accepts the oversell gap) and any payment handling.
/// </summary>
public sealed class CreateOrderCommandHandler
    : ICommandHandler<CreateOrderCommand, CreateOrderResult>
{
    private readonly ICustomerRepository _customers;
    private readonly IProductRepository _products;
    private readonly IOrderRepository _orders;

    public CreateOrderCommandHandler(
        ICustomerRepository customers,
        IProductRepository products,
        IOrderRepository orders)
    {
        _customers = customers;
        _products = products;
        _orders = orders;
    }

    public async Task<CreateOrderResult> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Items is null || command.Items.Count == 0)
        {
            throw new ValidationException("An order must contain at least one item.");
        }

        if (command.Items.Any(i => i.Quantity <= 0))
        {
            throw new ValidationException("Every order item must have a quantity greater than zero.");
        }

        if (command.Items.Any(i => i.ProductId <= 0))
        {
            throw new ValidationException("Every order item must reference a valid product.");
        }

        // Repeat purchases of the same product collapse into one line with the summed
        // quantity (schema v1, §4 — UNIQUE(order_id, product_id)).
        var requestedQuantities = command.Items
            .GroupBy(i => i.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));

        if (!await _customers.ExistsAsync(command.CustomerId, cancellationToken))
        {
            throw new NotFoundException("Customer", command.CustomerId);
        }

        var productIds = requestedQuantities.Keys.ToArray();
        var products = await _products.GetByIdsAsync(productIds, cancellationToken);
        var productsById = products.ToDictionary(p => p.Id);

        var missing = productIds.Where(id => !productsById.ContainsKey(id)).ToArray();
        if (missing.Length > 0)
        {
            throw new NotFoundException("Product", string.Join(", ", missing));
        }

        var discontinued = products
            .Where(p => p.Status != ProductStatus.Active)
            .Select(p => p.Id)
            .ToArray();
        if (discontinued.Length > 0)
        {
            throw new ValidationException(
                $"These products are not available for ordering: {string.Join(", ", discontinued)}.");
        }

        var items = requestedQuantities
            .Select(kvp => OrderItem.Create(kvp.Key, kvp.Value, productsById[kvp.Key].Price))
            .ToList();

        var order = Order.Create(command.CustomerId, items);

        await _orders.AddAsync(order, cancellationToken);

        return new CreateOrderResult(order.Id, order.TotalAmount, order.Status.ToString());
    }
}
