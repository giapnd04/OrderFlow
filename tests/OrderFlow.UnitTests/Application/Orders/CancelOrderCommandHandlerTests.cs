using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.CancelOrder;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Orders;

public sealed class CancelOrderCommandHandlerTests
{
    private static Product ReservedProduct(int id, int stock, int reserveQty)
    {
        var product = Product.Create($"SKU-{id}", $"Product {id}", 100m, stockQuantity: stock);
        product.Id = id;
        product.Reserve(reserveQty);
        return product;
    }

    private static Order PendingOrder(Product product, int qty)
    {
        var order = Order.Create(1, new[] { OrderItem.Create(product.Id, qty, product.Price) });
        order.Id = 42;
        return order;
    }

    [Fact]
    public async Task Handle_PendingPaymentOrder_ReleasesReservationAndCancels()
    {
        var product = ReservedProduct(10, stock: 20, reserveQty: 3);
        var order = PendingOrder(product, 3);
        var handler = new CancelOrderCommandHandler(new FakeOrderRepository(order), new FakeProductRepository(product));

        var result = await handler.Handle(new CancelOrderCommand(order.Id));

        Assert.Equal(nameof(OrderStatus.Cancelled), result.Status);
        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(20, product.StockQuantity);
    }

    [Fact]
    public async Task Handle_ConfirmedOrder_RestocksInsteadOfReleasing()
    {
        var product = ReservedProduct(10, stock: 20, reserveQty: 3);
        var order = PendingOrder(product, 3);
        order.MarkAsPaid();
        order.Confirm();
        var handler = new CancelOrderCommandHandler(new FakeOrderRepository(order), new FakeProductRepository(product));

        var result = await handler.Handle(new CancelOrderCommand(order.Id));

        Assert.Equal(nameof(OrderStatus.Cancelled), result.Status);
        Assert.Equal(3, product.ReservedQuantity);
        Assert.Equal(23, product.StockQuantity);
    }

    [Fact]
    public async Task Handle_PaidOrder_ThrowsInvalidOrderState()
    {
        var product = ReservedProduct(10, stock: 20, reserveQty: 3);
        var order = PendingOrder(product, 3);
        order.MarkAsPaid();
        var handler = new CancelOrderCommandHandler(new FakeOrderRepository(order), new FakeProductRepository(product));

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => handler.Handle(new CancelOrderCommand(order.Id)));
        Assert.Equal(3, product.ReservedQuantity);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new CancelOrderCommandHandler(new FakeOrderRepository(order: null), new FakeProductRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CancelOrderCommand(999)));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = new CancelOrderCommandHandler(new FakeOrderRepository(order: null), new FakeProductRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new CancelOrderCommand(0)));
    }

    [Fact]
    public async Task Handle_ProductNoLongerFound_ThrowsNotFound()
    {
        var product = ReservedProduct(10, stock: 20, reserveQty: 3);
        var order = PendingOrder(product, 3);
        var handler = new CancelOrderCommandHandler(new FakeOrderRepository(order), new FakeProductRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new CancelOrderCommand(order.Id)));
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly Order? _order;

        public FakeOrderRepository(Order? order) => _order = order;

        public Task AddAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_order is not null && _order.Id == orderId ? _order : null);

        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(
            OrderStatus? status, int? customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> _products;

        public FakeProductRepository(params Product[] products) => _products = products.ToList();

        public Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyCollection<int> productIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(_products.Where(p => productIds.Contains(p.Id)).ToList());

        public Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Product?> GetByIdForUpdateAsync(int productId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<(IReadOnlyCollection<Product> Products, int TotalCount)> GetPagedAsync(
            string? search, ProductStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddAsync(Product product, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
