using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Products.CreateProduct;

public sealed record CreateProductResult(
    int ProductId,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    ProductStatus Status);