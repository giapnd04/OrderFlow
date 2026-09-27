namespace OrderFlow.Application.Features.Orders.GetOrderStatusSummary;

public sealed record GetOrderStatusSummaryResult(
    IReadOnlyCollection<GetOrderStatusSummaryItemResult> Statuses,
    int TotalOrderCount,
    decimal TotalAmount);

public sealed record GetOrderStatusSummaryItemResult(
    string Status,
    int OrderCount,
    decimal TotalAmount);
