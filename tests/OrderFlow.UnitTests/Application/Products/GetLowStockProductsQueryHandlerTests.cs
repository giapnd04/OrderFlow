using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Products.GetLowStockProducts;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Products;

public sealed class GetLowStockProductsQueryHandlerTests
{
    private static Product ProductWith(int id, int stock, int reserved)
    {
        var product = Product.Create($"SKU-{id}", $"Product {id}", 100m, stockQuantity: stock);
        product.Id = id;
        if (reserved > 0)
        {
            product.Reserve(reserved);
        }

        return product;
    }

    [Fact]
    public async Task Handle_ReturnsProductsWithAvailableQuantityComputed()
    {
        var handler = new GetLowStockProductsQueryHandler(
            new FakeProductReportRepository(ProductWith(1, stock: 10, reserved: 7)));

        var result = await handler.Handle(new GetLowStockProductsQuery(Threshold: 5));

        Assert.Equal(5, result.Threshold);
        var item = Assert.Single(result.Items);
        Assert.Equal(10, item.StockQuantity);
        Assert.Equal(7, item.ReservedQuantity);
        Assert.Equal(3, item.AvailableQuantity);
    }

    [Fact]
    public async Task Handle_PassesThresholdAndLimitToRepository()
    {
        var reports = new FakeProductReportRepository();
        var handler = new GetLowStockProductsQueryHandler(reports);

        await handler.Handle(new GetLowStockProductsQuery(Threshold: 3, Limit: 20));

        Assert.Equal(3, reports.LastThreshold);
        Assert.Equal(20, reports.LastLimit);
    }

    [Fact]
    public async Task Handle_NoLowStock_ReturnsEmptyList()
    {
        var handler = new GetLowStockProductsQueryHandler(new FakeProductReportRepository());

        var result = await handler.Handle(new GetLowStockProductsQuery());

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Handle_NegativeThreshold_ThrowsValidation()
    {
        var handler = new GetLowStockProductsQueryHandler(new FakeProductReportRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetLowStockProductsQuery(Threshold: -1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_LimitOutOfRange_ThrowsValidation(int limit)
    {
        var handler = new GetLowStockProductsQueryHandler(new FakeProductReportRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetLowStockProductsQuery(Limit: limit)));
    }

    private sealed class FakeProductReportRepository : IProductReportRepository
    {
        private readonly IReadOnlyList<Product> _products;

        public int? LastThreshold { get; private set; }

        public int? LastLimit { get; private set; }

        public FakeProductReportRepository(params Product[] products) => _products = products;

        public Task<IReadOnlyList<Product>> GetLowStockAsync(int threshold, int limit, CancellationToken cancellationToken = default)
        {
            LastThreshold = threshold;
            LastLimit = limit;
            return Task.FromResult(_products);
        }
    }
}
