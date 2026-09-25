namespace OrderFlow.Application.Features.Products.RestockProduct;

public sealed record RestockProductResult(int ProductId, int StockQuantity, int ReservedQuantity);
