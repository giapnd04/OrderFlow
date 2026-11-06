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
        // One round trip instead of three: COUNT, a conditional SUM and MAX over the same rows.
        // Grouping by a constant is EF's way of asking for several aggregates in one SELECT.
        var totals = await _db.Orders
            .AsNoTracking()
            .Where(o => o.CustomerId == customerId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                OrderCount = g.Count(),
                Spent = g.Sum(o => PaidStatuses.Contains(o.Status) ? o.TotalAmount : 0m),
                LastOrderAt = g.Max(o => (DateTime?)o.CreatedAt),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // No orders at all means no group, hence no row.
        return totals is null
            ? new CustomerOrderTotals(0, 0m, null)
            : new CustomerOrderTotals(totals.OrderCount, totals.Spent, totals.LastOrderAt);
    }
}
