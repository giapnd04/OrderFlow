using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.GetProducts;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class GetProductsQueryHandlerTests
{
    private static Product ProductWith(int id, string sku)
    {
        var product = Product.Create(sku, $"Product {id}", 100m, stockQuantity: 10);
        product.Id = id;
        return product;
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsPagedProducts()
    {
        var handler = new GetProductsQueryHandler(
            new FakeProductRepository(ProductWith(1, "SKU-1"), ProductWith(2, "SKU-2"), ProductWith(3, "SKU-3")));

        var result = await handler.Handle(new GetProductsQuery(PageNumber: 1, PageSize: 2));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("SKU-1", result.Items.First().Sku);
        Assert.Equal(nameof(ProductStatus.Active), result.Items.First().Status);
    }

    [Fact]
    public async Task Handle_ZeroPageSize_ThrowsValidation()
    {
        var handler = new GetProductsQueryHandler(new FakeProductRepository());

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetProductsQuery(PageNumber: 1, PageSize: 0)));
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_ThrowsValidation()
    {
        var handler = new GetProductsQueryHandler(new FakeProductRepository());

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetProductsQuery(PageNumber: 1, PageSize: 101)));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> _products;

        public FakeProductRepository(params Product[] products) => _products = products.ToList();

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProducts tests.");

        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProducts tests.");

        public Task<Product?> GetByIdForUpdateAsync(int productId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProducts tests.");

        public Task<(IReadOnlyCollection<Product> Products, int TotalCount)> GetPagedAsync(
            string? search, ProductStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var page = _products.OrderBy(p => p.Id).Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return Task.FromResult<(IReadOnlyCollection<Product>, int)>((page, _products.Count));
        }

        public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProducts tests.");

        public Task AddAsync(Product product, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProducts tests.");

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetProducts tests.");
    }
}
