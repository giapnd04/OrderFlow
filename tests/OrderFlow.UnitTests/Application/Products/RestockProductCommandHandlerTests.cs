using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.RestockProduct;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class RestockProductCommandHandlerTests
{
    private static Product ProductWithReservation()
    {
        var product = Product.Create("SKU-001", "Laptop", 100m, stockQuantity: 10);
        product.Id = 1;
        product.Reserve(4);
        return product;
    }

    [Fact]
    public async Task Handle_ValidQuantity_IncreasesStockAndKeepsReservation()
    {
        var product = ProductWithReservation();
        var handler = new RestockProductCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new RestockProductCommand(product.Id, 5));

        Assert.Equal(15, result.StockQuantity);
        Assert.Equal(4, result.ReservedQuantity);
        Assert.Equal(15, product.StockQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task Handle_NonPositiveQuantity_ThrowsValidation(int quantity)
    {
        var product = ProductWithReservation();
        var handler = new RestockProductCommandHandler(new FakeProductRepository(product));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new RestockProductCommand(product.Id, quantity)));

        Assert.Equal(10, product.StockQuantity);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = new RestockProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RestockProductCommand(999, 5)));
    }

    [Fact]
    public async Task Handle_InvalidProductId_ThrowsValidation()
    {
        var handler = new RestockProductCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new RestockProductCommand(0, 5)));
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
