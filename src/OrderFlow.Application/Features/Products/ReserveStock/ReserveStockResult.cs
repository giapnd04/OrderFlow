namespace OrderFlow.Application.Features.Products.ReserveStock;

public sealed record ReserveStockResult(int ProductId, int StockQuantity, int ReservedQuantity, int AvailableQuantity);
