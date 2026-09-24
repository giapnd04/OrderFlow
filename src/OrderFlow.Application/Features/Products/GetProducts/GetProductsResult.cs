namespace OrderFlow.Application.Features.Products.GetProducts;

public sealed record GetProductsResult(
    IReadOnlyCollection<GetProductsItemResult> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record GetProductsItemResult(
    int ProductId,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    int ReservedQuantity,
    string Status);
