using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class OrderReportRepository : IOrderReportRepository
{
    private static readonly OrderStatus[] PaidStatuses =
    [
        OrderStatus.Paid,
        OrderStatus.Confirmed,
        OrderStatus.Shipped,
        OrderStatus.Delivered,
    ];

    private readonly OrderFlowDbContext _db;

    public OrderReportRepository(OrderFlowDbContext db) => _db = db;

    public async Task<IReadOnlyList<OrderStatusTotals>> GetStatusTotalsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Orders
            .AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(o => o.TotalAmount) })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new OrderStatusTotals(r.Status, r.Count, r.Total))
            .ToList();
    }

    public async Task<CustomerOrderTotals> GetCustomerTotalsAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        var orders = _db.Orders.AsNoTracking().Where(o => o.CustomerId == customerId);

        var orderCount = await orders.CountAsync(cancellationToken);

        var spent = await orders
            .Where(o => PaidStatuses.Contains(o.Status))
            .SumAsync(o => (decimal?)o.TotalAmount, cancellationToken) ?? 0m;

        var lastOrderAt = await orders.MaxAsync(o => (DateTime?)o.CreatedAt, cancellationToken);

        return new CustomerOrderTotals(orderCount, spent, lastOrderAt);
    }
}
