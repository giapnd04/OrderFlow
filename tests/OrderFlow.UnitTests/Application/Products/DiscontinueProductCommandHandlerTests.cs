using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.DiscontinueProduct;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class DiscontinueProductCommandHandlerTests
{
    private static Product ActiveProduct()
    {
        var product = Product.Create("SKU-001", "Laptop", 100m, stockQuantity: 10);
        product.Id = 1;
        return product;
    }

    [Fact]
    public async Task Handle_ActiveProduct_TransitionsToDiscontinued()
    {
        var product = ActiveProduct();
        var handler = new DiscontinueProductCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new DiscontinueProductCommand(product.Id));

        Assert.Equal(nameof(ProductStatus.Discontinued), result.Status);
        Assert.Equal(ProductStatus.Discontinued, product.Status);
    }

    [Fact]
    public async Task Handle_AlreadyDiscontinued_IsIdempotent()
    {
        var product = ActiveProduct();
        product.Discontinue();
        var handler = new DiscontinueProductCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new DiscontinueProductCommand(product.Id));

        Assert.Equal(nameof(ProductStatus.Discontinued), result.Status);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = new DiscontinueProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DiscontinueProductCommand(999)));
    }

    [Fact]
    public async Task Handle_InvalidProductId_ThrowsValidation()
    {
        var handler = new DiscontinueProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new DiscontinueProductCommand(0)));
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
