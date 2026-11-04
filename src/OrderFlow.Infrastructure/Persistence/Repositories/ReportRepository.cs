using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

// Status values are literals on purpose: a filtered index (WHERE status = N'Succeeded') can only
// be used when the query states that same literal, not a parameter with an unknown value.
internal sealed class ReportRepository : IReportRepository
{
    private readonly OrderFlowDbContext _db;

    public ReportRepository(OrderFlowDbContext db) => _db = db;

    public async Task<IReadOnlyList<MonthlyRevenueRow>> GetMonthlyRevenueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        return await _db.Database.SqlQuery<MonthlyRevenueRow>($"""
            SELECT YEAR(created_at) AS [Year],
                   MONTH(created_at) AS [Month],
                   COUNT(*) AS PaymentCount,
                   SUM(amount) AS Revenue
            FROM payment_attempts
            WHERE status = N'Succeeded'
              AND created_at >= {fromUtc}
              AND created_at < {toUtc}
            GROUP BY YEAR(created_at), MONTH(created_at)
            ORDER BY [Year], [Month]
            """).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TopCustomerRow>> GetTopCustomersAsync(int top, CancellationToken cancellationToken = default)
    {
        return await _db.Database.SqlQuery<TopCustomerRow>($"""
            WITH paid AS (
                SELECT o.customer_id, SUM(p.amount) AS paid_amount, COUNT(*) AS payment_count
                FROM payment_attempts p
                JOIN orders o ON o.id = p.order_id
                WHERE p.status = N'Succeeded'
                GROUP BY o.customer_id
            ),
            ranked AS (
                SELECT customer_id, paid_amount, payment_count,
                       RANK() OVER (ORDER BY paid_amount DESC) AS customer_rank
                FROM paid
            )
            SELECT r.customer_rank AS [Rank],
                   r.customer_id AS CustomerId,
                   c.name AS CustomerName,
                   r.paid_amount AS PaidAmount,
                   r.payment_count AS PaymentCount
            FROM ranked r
            JOIN customers c ON c.id = r.customer_id
            WHERE r.customer_rank <= {top}
            ORDER BY r.customer_rank, r.customer_id
            """).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<StalePendingOrderRow>> GetStalePendingOrdersAsync(
        DateTime cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _db.Database.SqlQuery<StalePendingOrderRow>($"""
            SELECT TOP ({limit})
                   id AS OrderId,
                   customer_id AS CustomerId,
                   total_amount AS TotalAmount,
                   created_at AS CreatedAt
            FROM orders
            WHERE status = N'PendingPayment'
              AND created_at < {cutoffUtc}
            ORDER BY created_at, id
            """).ToListAsync(cancellationToken);
    }
}
