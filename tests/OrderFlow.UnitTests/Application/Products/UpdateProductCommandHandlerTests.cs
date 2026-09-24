using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.UpdateProduct;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class UpdateProductCommandHandlerTests
{
    private static Product ExistingProduct()
    {
        var product = Product.Create("SKU-001", "Laptop", 100m, stockQuantity: 10);
        product.Id = 1;
        return product;
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesNameAndPriceButNotSkuOrStock()
    {
        var product = ExistingProduct();
        var handler = new UpdateProductCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new UpdateProductCommand(product.Id, "  Gaming Laptop ", 150m));

        Assert.Equal("Gaming Laptop", result.Name);
        Assert.Equal(150m, result.Price);
        Assert.Equal("SKU-001", result.Sku);
        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = new UpdateProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new UpdateProductCommand(999, "Name", 10m)));
    }

    [Fact]
    public async Task Handle_NegativePrice_ThrowsDomainException()
    {
        var product = ExistingProduct();
        var handler = new UpdateProductCommandHandler(new FakeProductRepository(product));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new UpdateProductCommand(product.Id, "Laptop", -1m)));
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsDomainException()
    {
        var product = ExistingProduct();
        var handler = new UpdateProductCommandHandler(new FakeProductRepository(product));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new UpdateProductCommand(product.Id, " ", 10m)));
    }

    [Fact]
    public async Task Handle_InvalidProductId_ThrowsValidation()
    {
        var handler = new UpdateProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new UpdateProductCommand(0, "Name", 10m)));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly Product? _product;

        public FakeProductRepository(Product? product) => _product = product;

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateProduct tests.");

        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateProduct tests.");

        public Task<Product?> GetByIdForUpdateAsync(int productId, CancellationToken cancellationToken = default)
            => Task.FromResult(_product is not null && _product.Id == productId ? _product : null);

        public Task<(IReadOnlyCollection<Product> Products, int TotalCount)> GetPagedAsync(
            string? search, ProductStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateProduct tests.");

        public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateProduct tests.");

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateProduct tests.");

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
