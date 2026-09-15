namespace OrderFlow.Application.Features.Orders.GetOrders;

public sealed record GetOrdersResult(
    IReadOnlyCollection<GetOrdersItemResult> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record GetOrdersItemResult(
    int OrderId,
    int CustomerId,
    string Status,
    decimal TotalAmount);