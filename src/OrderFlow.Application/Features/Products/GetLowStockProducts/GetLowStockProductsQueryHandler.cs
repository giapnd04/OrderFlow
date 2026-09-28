using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Products.GetLowStockProducts;

public sealed class GetLowStockProductsQueryHandler
    : IQueryHandler<GetLowStockProductsQuery, GetLowStockProductsResult>
{
    private const int MaxLimit = 100;

    private readonly IProductReportRepository _reports;

    public GetLowStockProductsQueryHandler(IProductReportRepository reports)
    {
        _reports = reports;
    }

    public async Task<GetLowStockProductsResult> Handle(
        GetLowStockProductsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.Threshold < 0)
        {
            throw new ValidationException("Threshold cannot be negative.");
        }

        if (query.Limit <= 0)
        {
            throw new ValidationException("Limit must be greater than zero.");
        }

        if (query.Limit > MaxLimit)
        {
            throw new ValidationException($"Limit cannot be greater than {MaxLimit}.");
        }

        var products = await _reports.GetLowStockAsync(query.Threshold, query.Limit, cancellationToken);

        var items = products
            .Select(p => new GetLowStockProductsItemResult(
                p.Id,
                p.Sku,
                p.Name,
                p.StockQuantity,
                p.ReservedQuantity,
                p.StockQuantity - p.ReservedQuantity))
            .ToList();

        return new GetLowStockProductsResult(query.Threshold, items);
    }
}
