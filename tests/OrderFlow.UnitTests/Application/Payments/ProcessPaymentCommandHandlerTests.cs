using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Payments;

public sealed class ProcessPaymentCommandHandlerTests
{
    private static Product ReservedProduct(int id, int reserveQty, decimal price)
    {
        var product = Product.Create($"SKU-{id}", $"Product {id}", price, stockQuantity: 100);
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

    private static ProcessPaymentCommandHandler HandlerFor(
        FakeOrderRepository orders, FakePaymentRepository payments, params Product[] products)
        => new(orders, payments, new FakeProductRepository(products));

    [Fact]
    public async Task Handle_SingleSucceededPaymentCoveringTotal_MarksOrderPaidAndFulfillsReservation()
    {
        var product = ReservedProduct(10, reserveQty: 2, price: 100m);
        var order = PendingOrder(product, 2);
        var orders = new FakeOrderRepository(order);
        var payments = new FakePaymentRepository();

        var result = await HandlerFor(orders, payments, product).Handle(
            new ProcessPaymentCommand(order.Id, 200m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, result.OrderStatus);
        Assert.Equal(OrderStatus.Paid, order.Status);
        Assert.Equal(0, product.ReservedQuantity);
        Assert.Equal(98, product.StockQuantity);
    }

    [Fact]
    public async Task Handle_PartialSucceededPayment_DoesNotMarkOrderPaid()
    {
        var product = ReservedProduct(10, reserveQty: 2, price: 100m);
        var order = PendingOrder(product, 2);
        var orders = new FakeOrderRepository(order);
        var payments = new FakePaymentRepository();

        var result = await HandlerFor(orders, payments, product).Handle(
            new ProcessPaymentCommand(order.Id, 50m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded),
            CancellationToken.None);

        Assert.Equal(OrderStatus.PendingPayment, result.OrderStatus);
        Assert.Equal(OrderStatus.PendingPayment, order.Status);
        Assert.Equal(2, product.ReservedQuantity);
    }

    [Fact]
    public async Task Handle_TwoPartialPaymentsSummingToTotal_MarksOrderPaidOnSecondAttempt()
    {
        var product = ReservedProduct(10, reserveQty: 2, price: 100m);
        var order = PendingOrder(product, 2);
        var orders = new FakeOrderRepository(order);
        var payments = new FakePaymentRepository();
        var handler = HandlerFor(orders, payments, product);

        var first = await handler.Handle(
            new ProcessPaymentCommand(order.Id, 100m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded),
            CancellationToken.None);
        Assert.Equal(OrderStatus.PendingPayment, first.OrderStatus);

        var second = await handler.Handle(
            new ProcessPaymentCommand(order.Id, 100m, "Stripe", "tx-2", PaymentAttemptStatus.Succeeded),
            CancellationToken.None);

        Assert.Equal(OrderStatus.Paid, second.OrderStatus);
    }

    [Fact]
    public async Task Handle_FailedPayment_DoesNotMarkOrderPaidOrFulfill()
    {
        var product = ReservedProduct(10, reserveQty: 2, price: 100m);
        var order = PendingOrder(product, 2);
        var orders = new FakeOrderRepository(order);
        var payments = new FakePaymentRepository();

        var result = await HandlerFor(orders, payments, product).Handle(
            new ProcessPaymentCommand(order.Id, 200m, "Stripe", "tx-1", PaymentAttemptStatus.Failed),
            CancellationToken.None);

        Assert.Equal(OrderStatus.PendingPayment, result.OrderStatus);
        Assert.Equal(2, product.ReservedQuantity);
        Assert.Equal(PaymentAttemptStatus.Failed, result.PaymentAttemptStatus);
    }

    [Fact]
    public async Task Handle_OrderNotPendingPayment_ThrowsInvalidOrderState()
    {
        var product = ReservedProduct(10, reserveQty: 2, price: 100m);
        var order = PendingOrder(product, 2);
        order.MarkAsPaid();
        var orders = new FakeOrderRepository(order);

        await Assert.ThrowsAsync<InvalidOrderStateException>(() => HandlerFor(orders, new FakePaymentRepository(), product)
            .Handle(new ProcessPaymentCommand(order.Id, 200m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = HandlerFor(new FakeOrderRepository(order: null), new FakePaymentRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(
            new ProcessPaymentCommand(999, 100m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = HandlerFor(new FakeOrderRepository(order: null), new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new ProcessPaymentCommand(0, 100m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_NonPositiveAmount_ThrowsValidation()
    {
        var handler = HandlerFor(new FakeOrderRepository(order: null), new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new ProcessPaymentCommand(1, 0m, "Stripe", "tx-1", PaymentAttemptStatus.Succeeded), CancellationToken.None));
    }

    [Fact]
    public async Task Handle_EmptyProvider_ThrowsValidation()
    {
        var handler = HandlerFor(new FakeOrderRepository(order: null), new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new ProcessPaymentCommand(1, 100m, " ", "tx-1", PaymentAttemptStatus.Succeeded), CancellationToken.None));
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

    /// <summary>
    /// Mirrors the real <c>PaymentRepository</c>: <see cref="AddAsync"/> persists immediately
    /// (the real implementation calls SaveChanges inline), so a subsequent
    /// <see cref="GetSucceededAmountAsync"/> in the same handler call already includes it.
    /// </summary>
    private sealed class FakePaymentRepository : IPaymentRepository
    {
        private readonly List<PaymentAttempt> _attempts = new();
        private int _nextId = 1;

        public Task AddAsync(PaymentAttempt paymentAttempt, CancellationToken cancellationToken = default)
        {
            paymentAttempt.Id = _nextId++;
            _attempts.Add(paymentAttempt);
            return Task.CompletedTask;
        }

        public Task<PaymentAttempt?> GetByIdAsync(int paymentAttemptId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<PaymentAttempt>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<(IReadOnlyCollection<PaymentAttempt> Payments, int TotalCount)> GetPagedAsync(
            PaymentAttemptStatus? status, string? provider, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<decimal> GetSucceededAmountAsync(int orderId, CancellationToken cancellationToken = default)
            => Task.FromResult(_attempts
                .Where(a => a.OrderId == orderId && a.Status == PaymentAttemptStatus.Succeeded)
                .Sum(a => a.Amount));
    }
}
