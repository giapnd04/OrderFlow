namespace OrderFlow.API.Models.Products;

public sealed record CreateProductRequest(
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity);