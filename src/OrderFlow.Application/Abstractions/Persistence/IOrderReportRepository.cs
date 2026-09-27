using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Read-only aggregate queries over orders for reporting. Kept apart from
/// <see cref="IOrderRepository"/>, which is the write/aggregate-loading side.
/// </summary>
public interface IOrderReportRepository
{
    Task<IReadOnlyList<OrderStatusTotals>> GetStatusTotalsAsync(CancellationToken cancellationToken = default);

    Task<CustomerOrderTotals> GetCustomerTotalsAsync(int customerId, CancellationToken cancellationToken = default);
}

public sealed record OrderStatusTotals(OrderStatus Status, int OrderCount, decimal TotalAmount);

/// <summary>
/// SpentAmount only counts orders that were actually paid for
/// (Paid, Confirmed, Shipped, Delivered) — not PendingPayment or Cancelled.
/// </summary>
public sealed record CustomerOrderTotals(int OrderCount, decimal SpentAmount, DateTime? LastOrderAt);
