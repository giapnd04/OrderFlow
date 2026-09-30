using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.ReserveStock;

/// <summary>
/// Manually reserves stock for a product outside the normal CreateOrder flow (e.g. a
/// manual hold negotiated off-system). Uses the same <see cref="Domain.Entities.Product.Reserve"/>
/// invariant CreateOrder relies on, so it can never oversell relative to available stock.
/// </summary>
public sealed class ReserveStockCommandHandler : ICommandHandler<ReserveStockCommand, ReserveStockResult>
{
    private readonly IProductRepository _products;

    public ReserveStockCommandHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<ReserveStockResult> Handle(
        ReserveStockCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ProductId <= 0)
        {
            throw new ValidationException("ProductId must be greater than zero.");
        }

        if (command.Quantity <= 0)
        {
            throw new ValidationException("Quantity must be greater than zero.");
        }

        var product = await _products.GetByIdForUpdateAsync(command.ProductId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", command.ProductId);
        }

        product.Reserve(command.Quantity);

        await _products.UpdateAsync(product, cancellationToken);

        return new ReserveStockResult(
            product.Id,
            product.StockQuantity,
            product.ReservedQuantity,
            product.StockQuantity - product.ReservedQuantity);
    }
}
