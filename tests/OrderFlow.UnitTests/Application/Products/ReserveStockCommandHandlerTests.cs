using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.ReserveStock;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class ReserveStockCommandHandlerTests
{
    private static Product ProductWithStock(int stock = 10)
    {
        var product = Product.Create("SKU-001", "Laptop", 100m, stockQuantity: stock);
        product.Id = 1;
        return product;
    }

    [Fact]
    public async Task Handle_SufficientAvailableStock_IncreasesReservedQuantity()
    {
        var product = ProductWithStock(10);
        var handler = new ReserveStockCommandHandler(new FakeProductRepository(product));

        var result = await handler.Handle(new ReserveStockCommand(product.Id, 4));

        Assert.Equal(10, result.StockQuantity);
        Assert.Equal(4, result.ReservedQuantity);
        Assert.Equal(6, result.AvailableQuantity);
        Assert.Equal(4, product.ReservedQuantity);
    }

    [Fact]
    public async Task Handle_QuantityExceedsAvailableStock_ThrowsInsufficientStock()
    {
        var product = ProductWithStock(5);
        product.Reserve(3);
        var handler = new ReserveStockCommandHandler(new FakeProductRepository(product));

        await Assert.ThrowsAsync<InsufficientStockException>(
            () => handler.Handle(new ReserveStockCommand(product.Id, 3)));

        Assert.Equal(3, product.ReservedQuantity);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Handle_NonPositiveQuantity_ThrowsValidation(int quantity)
    {
        var product = ProductWithStock();
        var handler = new ReserveStockCommandHandler(new FakeProductRepository(product));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new ReserveStockCommand(product.Id, quantity)));

        Assert.Equal(0, product.ReservedQuantity);
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = new ReserveStockCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ReserveStockCommand(999, 1)));
    }

    [Fact]
    public async Task Handle_InvalidProductId_ThrowsValidation()
    {
        var handler = new ReserveStockCommandHandler(new FakeProductRepository(product: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReserveStockCommand(0, 1)));
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
