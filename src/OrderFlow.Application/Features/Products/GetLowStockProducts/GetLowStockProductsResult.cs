namespace OrderFlow.Application.Features.Products.GetLowStockProducts;

public sealed record GetLowStockProductsResult(
    int Threshold,
    IReadOnlyCollection<GetLowStockProductsItemResult> Items);

/// <summary>AvailableQuantity = StockQuantity - ReservedQuantity.</summary>
public sealed record GetLowStockProductsItemResult(
    int ProductId,
    string Sku,
    string Name,
    int StockQuantity,
    int ReservedQuantity,
    int AvailableQuantity);
