using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.ReactivateProduct;

public sealed class ReactivateProductCommandHandler
    : ICommandHandler<ReactivateProductCommand, ReactivateProductResult>
{
    private readonly IProductRepository _products;

    public ReactivateProductCommandHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<ReactivateProductResult> Handle(
        ReactivateProductCommand command,
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

        product.Reactivate();

        await _products.UpdateAsync(product, cancellationToken);

        return new ReactivateProductResult(product.Id, product.Status.ToString());
    }
}
