namespace OrderFlow.Application.Features.Products.GetProductById;

/// <summary>
/// Outcome of a successful <see cref="GetProductByIdQuery"/>. A flat contract the API
/// can return directly without exposing the <c>Product</c> domain entity (SDS §11).
/// </summary>
public sealed record GetProductByIdResult(
    int ProductId,
    string Sku,
    string Name,
    decimal Price,
    int StockQuantity,
    int ReservedQuantity,
    string Status);
