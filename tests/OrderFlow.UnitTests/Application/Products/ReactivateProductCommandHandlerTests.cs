using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.ReactivateProduct;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class ReactivateProductCommandHandlerTests
{
    private static Product DiscontinuedProduct()
    {
        var product = Product.Create("SKU-001", "Laptop", 100m, stockQuantity: 10);
        product.Id = 1;
        product.Discontinue();
        return product;
    }

    [Fact]
    public async Task Handle_DiscontinuedProduct_TransitionsToActive()
    {
        var product = DiscontinuedProduct();
        var handler = new ReactivateProductCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new ReactivateProductCommand(product.Id));

        Assert.Equal(nameof(ProductStatus.Active), result.Status);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public async Task Handle_AlreadyActive_IsIdempotent()
    {
        var product = Product.Create("SKU-001", "Laptop", 100m, stockQuantity: 10);
        product.Id = 1;
        var handler = new ReactivateProductCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new ReactivateProductCommand(product.Id));

        Assert.Equal(nameof(ProductStatus.Active), result.Status);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = new ReactivateProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ReactivateProductCommand(999)));
    }

    [Fact]
    public async Task Handle_InvalidProductId_ThrowsValidation()
    {
        var handler = new ReactivateProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReactivateProductCommand(0)));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly Product? _product;

        public FakeProductRepository(Product? product) => _product = product;

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Product?> GetByIdForUpdateAsync(int productId, CancellationToken cancellationToken = default)
            => Task.FromResult(_product is not null && _product.Id == productId ? _product : null);

        public Task<(IReadOnlyCollection<Product> Products, int TotalCount)> GetPagedAsync(
            string? search, ProductStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
