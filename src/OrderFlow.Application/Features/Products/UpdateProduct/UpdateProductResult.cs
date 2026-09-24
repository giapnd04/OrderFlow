namespace OrderFlow.Application.Features.Products.UpdateProduct;

public sealed record UpdateProductResult(int ProductId, string Sku, string Name, decimal Price);
