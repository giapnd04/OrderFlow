using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.CreateOrder;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Orders;

public class CreateOrderCommandHandlerTests
{
    private const int KnownCustomerId = 1;

    private static Product ActiveProduct(int id, decimal price) => new()
    {
        Id = id,
        Sku = $"SKU-{id}",
        Name = $"Product {id}",
        Price = price,
        StockQuantity = 100,
        Status = ProductStatus.Active,
    };

    private static CreateOrderCommandHandler HandlerFor(FakeOrderRepository orders, params Product[] products)
        => new(
            new FakeCustomerRepository(KnownCustomerId),
            new FakeProductRepository(products),
            orders);

    [Fact]
    public async Task Handle_ValidRequest_CreatesPendingOrderWithSnapshotPrices()
    {
        var orders = new FakeOrderRepository();
        var handler = HandlerFor(orders, ActiveProduct(10, 100m), ActiveProduct(20, 50m));

        var command = new CreateOrderCommand(KnownCustomerId, new[]
        {
            new CreateOrderItemInput(10, 2),
            new CreateOrderItemInput(20, 1),
        });

        var result = await handler.Handle(command);

        Assert.Equal(nameof(OrderStatus.PendingPayment), result.Status);
        Assert.Equal(250m, result.TotalAmount);

        Assert.NotNull(orders.Saved);
        Assert.Equal(OrderStatus.PendingPayment, orders.Saved!.Status);
        Assert.Equal(250m, orders.Saved.TotalAmount);
        Assert.Collection(
            orders.Saved.Items.OrderBy(i => i.ProductId),
            line => { Assert.Equal(10, line.ProductId); Assert.Equal(2, line.Quantity); Assert.Equal(100m, line.UnitPrice); },
            line => { Assert.Equal(20, line.ProductId); Assert.Equal(1, line.Quantity); Assert.Equal(50m, line.UnitPrice); });
    }

    [Fact]
    public async Task Handle_RepeatedProduct_MergesIntoOneLineWithSummedQuantity()
    {
        var orders = new FakeOrderRepository();
        var handler = HandlerFor(orders, ActiveProduct(10, 100m));

        var command = new CreateOrderCommand(KnownCustomerId, new[]
        {
            new CreateOrderItemInput(10, 2),
            new CreateOrderItemInput(10, 3),
        });

        var result = await handler.Handle(command);

        var line = Assert.Single(orders.Saved!.Items);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(500m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = HandlerFor(new FakeOrderRepository(), ActiveProduct(10, 100m));

        var command = new CreateOrderCommand(999, new[] { new CreateOrderItemInput(10, 1) });

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_UnknownProduct_ThrowsNotFound()
    {
        var handler = HandlerFor(new FakeOrderRepository(), ActiveProduct(10, 100m));

        var command = new CreateOrderCommand(KnownCustomerId, new[] { new CreateOrderItemInput(77, 1) });

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_DiscontinuedProduct_ThrowsValidation()
    {
        var discontinued = ActiveProduct(10, 100m);
        discontinued.Status = ProductStatus.Discontinued;
        var handler = HandlerFor(new FakeOrderRepository(), discontinued);

        var command = new CreateOrderCommand(KnownCustomerId, new[] { new CreateOrderItemInput(10, 1) });

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_NoItems_ThrowsValidation()
    {
        var handler = HandlerFor(new FakeOrderRepository(), ActiveProduct(10, 100m));

        var command = new CreateOrderCommand(KnownCustomerId, Array.Empty<CreateOrderItemInput>());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_NonPositiveQuantity_ThrowsValidation()
    {
        var handler = HandlerFor(new FakeOrderRepository(), ActiveProduct(10, 100m));

        var command = new CreateOrderCommand(KnownCustomerId, new[] { new CreateOrderItemInput(10, 0) });

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(command));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly HashSet<int> _ids;

        public FakeCustomerRepository(params int[] ids) => _ids = new HashSet<int>(ids);

        public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_ids.Contains(customerId));
    }

    private sealed class FakeProductRepository : IProductRepository
    {
        private readonly List<Product> _products;

        public FakeProductRepository(params Product[] products) => _products = products.ToList();

        public Task<IReadOnlyList<Product>> GetByIdsAsync(
            IReadOnlyCollection<int> productIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Product>>(
                _products.Where(p => productIds.Contains(p.Id)).ToList());
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        public Order? Saved { get; private set; }

        public Task AddAsync(Order order, CancellationToken cancellationToken = default)
        {
            Saved = order;
            return Task.CompletedTask;
        }

        public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
