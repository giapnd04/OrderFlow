namespace OrderFlow.Application.Features.Orders.GetCustomerOrderStats;

/// <summary>
/// SpentAmount counts only orders that were paid for (Paid, Confirmed, Shipped,
/// Delivered); PendingPayment and Cancelled orders are excluded.
/// </summary>
public sealed record GetCustomerOrderStatsResult(
    int CustomerId,
    int OrderCount,
    decimal SpentAmount,
    DateTime? LastOrderAt);
