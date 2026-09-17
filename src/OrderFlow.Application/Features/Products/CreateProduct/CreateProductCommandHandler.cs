using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Products.CreateProduct;

public sealed class CreateProductCommandHandler
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IProductRepository _products;

    public CreateProductCommandHandler(
        IProductRepository products)
    {
        _products = products;
    }

    public async Task<CreateProductResult> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken = default)
    {
        var sku = command.Sku?.Trim();

        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ValidationException("Product SKU is required.");
        }

        var name = command.Name?.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Product name is required.");
        }

        if (await _products.ExistsBySkuAsync(
                sku,
                cancellationToken))
        {
            throw new ValidationException($"Product with SKU '{sku}' already exists.");
        }

        var product = Product.Create(
            sku,
            name,
            command.Price,
            command.StockQuantity);

        await _products.AddAsync(
            product,
            cancellationToken);

        return new CreateProductResult(
            product.Id,
            product.Sku,
            product.Name,
            product.Price,
            product.StockQuantity,
            product.Status);
    }
}