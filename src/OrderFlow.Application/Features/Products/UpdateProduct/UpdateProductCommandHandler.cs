using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.UpdateProduct;

public sealed class UpdateProductCommandHandler : ICommandHandler<UpdateProductCommand, UpdateProductResult>
{
    private readonly IProductRepository _products;

    public UpdateProductCommandHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<UpdateProductResult> Handle(
        UpdateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ProductId <= 0)
        {
            throw new ValidationException("ProductId must be greater than zero.");
        }

        var product = await _products.GetByIdForUpdateAsync(command.ProductId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", command.ProductId);
        }

        product.UpdateDetails(command.Name, command.Price);

        await _products.UpdateAsync(product, cancellationToken);

        return new UpdateProductResult(product.Id, product.Sku, product.Name, product.Price);
    }
}
