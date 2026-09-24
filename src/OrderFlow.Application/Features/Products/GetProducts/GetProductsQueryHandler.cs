using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.GetProducts;

public sealed class GetProductsQueryHandler
    : IQueryHandler<GetProductsQuery, GetProductsResult>
{
    private const int MaxPageSize = 100;

    private readonly IProductRepository _products;

    public GetProductsQueryHandler(IProductRepository products)
    {
        _products = products;
    }

    public async Task<GetProductsResult> Handle(
        GetProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.PageNumber <= 0)
        {
            throw new ValidationException("PageNumber must be greater than zero.");
        }

        if (query.PageSize <= 0)
        {
            throw new ValidationException("PageSize must be greater than zero.");
        }

        if (query.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot be greater than {MaxPageSize}.");
        }

        var (products, totalCount) = await _products.GetPagedAsync(
            query.Search,
            query.Status,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / query.PageSize);

        var items = products
            .Select(product => new GetProductsItemResult(
                product.Id,
                product.Sku,
                product.Name,
                product.Price,
                product.StockQuantity,
                product.ReservedQuantity,
                product.Status.ToString()))
            .ToList();

        return new GetProductsResult(items, query.PageNumber, query.PageSize, totalCount, totalPages);
    }
}
