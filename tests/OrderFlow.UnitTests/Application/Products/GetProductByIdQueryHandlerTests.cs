using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.GetProductById;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Tests.Features.Products.GetProductById;

public sealed class GetProductByIdQueryHandlerTests
{
    private static Product ActiveProduct(int id)
    {
        var product = Product.Create("SKU-001", "Laptop", 25000000m, stockQuantity: 10);
        product.Id = id;
        product.Reserve(3);
        return product;
    }

    [Fact]
    public async Task Handle_ExistingProduct_ReturnsProductWithStockAndReservation()
    {
        var product = ActiveProduct(1);
        var handler = new GetProductByIdQueryHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new GetProductByIdQuery(product.Id));

        Assert.Equal(product.Id, result.ProductId);
        Assert.Equal("SKU-001", result.Sku);
        Assert.Equal("Laptop", result.Name);
        Assert.Equal(25000000m, result.Price);
        Assert.Equal(10, result.StockQuantity);
        Assert.Equal(3, result.ReservedQuantity);
        Assert.Equal(nameof(ProductStatus.Active), result.Status);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = new GetProductByIdQueryHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetProductByIdQuery(999)));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly Product? _product;

        public FakeProductRepository(Product? product) => _product = product;

        public Task<IReadOnlyList<Product>> GetByIdsAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProductById tests.");

        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
            => Task.FromResult(_product is not null && _product.Id == productId ? _product : null);

        public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProductById tests.");

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProductById tests.");
    }
}
