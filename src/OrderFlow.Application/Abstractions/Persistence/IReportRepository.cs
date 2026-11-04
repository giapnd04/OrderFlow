namespace OrderFlow.Application.Abstractions.Persistence;

public sealed record MonthlyRevenueRow(int Year, int Month, int PaymentCount, decimal Revenue);

public sealed record TopCustomerRow(long Rank, int CustomerId, string CustomerName, decimal PaidAmount, int PaymentCount);

public sealed record StalePendingOrderRow(int OrderId, int CustomerId, decimal TotalAmount, DateTime CreatedAt);

/// <summary>
/// Reporting reads (OF-08), written as SQL and backed by the indexes described in
/// docs/04-database-design.md. The same queries live in db/scripts/reports for measuring;
/// ReportQueriesTests fails if the two ever give different answers.
/// </summary>
public interface IReportRepository
{
    Task<IReadOnlyList<MonthlyRevenueRow>> GetMonthlyRevenueAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TopCustomerRow>> GetTopCustomersAsync(int top, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StalePendingOrderRow>> GetStalePendingOrdersAsync(
        DateTime cutoffUtc,
        int limit,
        CancellationToken cancellationToken = default);
}
