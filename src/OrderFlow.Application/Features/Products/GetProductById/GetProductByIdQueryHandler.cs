using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.GetProductById;

public sealed class GetProductByIdQueryHandler
    : IQueryHandler<GetProductByIdQuery, GetProductByIdResult>
{
    private readonly IProductRepository _products;

    public GetProductByIdQueryHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<GetProductByIdResult> Handle(
        GetProductByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var product = await _products.GetByIdAsync(query.ProductId, cancellationToken);

        if (product is null)
        {
            throw new NotFoundException("Product", query.ProductId);
        }

        return new GetProductByIdResult(
            product.Id,
            product.Sku,
            product.Name,
            product.Price,
            product.StockQuantity,
            product.ReservedQuantity,
            product.Status.ToString());
    }
}
