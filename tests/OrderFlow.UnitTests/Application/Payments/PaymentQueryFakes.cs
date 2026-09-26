using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Payments;

internal static class PaymentTestData
{
    public static Order OrderWithTotal(int id, decimal total)
    {
        var order = Order.Create(1, new[] { OrderItem.Create(10, 1, total) });
        order.Id = id;
        return order;
    }

    public static PaymentAttempt Attempt(int id, int orderId, decimal amount, PaymentAttemptStatus status)
    {
        var attempt = PaymentAttempt.Create(orderId, amount, "Stripe", status, $"tx-{id}");
        attempt.Id = id;
        return attempt;
    }
}

internal sealed class ReadOnlyFakeOrderRepository : IOrderRepository
{
    private readonly Order? _order;

    public ReadOnlyFakeOrderRepository(Order? order) => _order = order;

    public Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_order is not null && _order.Id == orderId ? _order : null);

    public Task AddAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpdateAsync(Order order, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(
        OrderStatus? status, int? customerId, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class FakePaymentRepository : IPaymentRepository
{
    private readonly List<PaymentAttempt> _attempts;

    public FakePaymentRepository(params PaymentAttempt[] attempts) => _attempts = attempts.ToList();

    public Task<IReadOnlyList<PaymentAttempt>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PaymentAttempt>>(_attempts.Where(a => a.OrderId == orderId).OrderBy(a => a.Id).ToList());

    public Task<decimal> GetSucceededAmountAsync(int orderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_attempts
            .Where(a => a.OrderId == orderId && a.Status == PaymentAttemptStatus.Succeeded)
            .Sum(a => a.Amount));

    public Task AddAsync(PaymentAttempt paymentAttempt, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
