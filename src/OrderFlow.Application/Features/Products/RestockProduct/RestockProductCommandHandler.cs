using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.RestockProduct;

public sealed class RestockProductCommandHandler
    : ICommandHandler<RestockProductCommand, RestockProductResult>
{
    private readonly IProductRepository _products;

    public RestockProductCommandHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<RestockProductResult> Handle(
        RestockProductCommand command,
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

        product.Restock(command.Quantity);

        await _products.UpdateAsync(product, cancellationToken);

        return new RestockProductResult(product.Id, product.StockQuantity, product.ReservedQuantity);
    }
}
