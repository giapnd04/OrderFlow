using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.DiscontinueProduct;

public sealed class DiscontinueProductCommandHandler
    : ICommandHandler<DiscontinueProductCommand, DiscontinueProductResult>
{
    private readonly IProductRepository _products;

    public DiscontinueProductCommandHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<DiscontinueProductResult> Handle(
        DiscontinueProductCommand command,
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

        product.Discontinue();

        await _products.UpdateAsync(product, cancellationToken);

        return new DiscontinueProductResult(product.Id, product.Status.ToString());
    }
}
