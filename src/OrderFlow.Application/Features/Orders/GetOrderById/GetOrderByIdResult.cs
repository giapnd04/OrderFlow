namespace OrderFlow.Application.Features.Orders.GetOrderById;

/// <summary>
/// Outcome of a successful <see cref="GetOrderByIdQuery"/>. A flat contract the API
/// can return directly without exposing the <c>Order</c> domain entity (SDS §11).
/// </summary>
public sealed record GetOrderByIdResult(
    int OrderId,
    int CustomerId,
    string Status,
    decimal TotalAmount,
    IReadOnlyCollection<GetOrderByIdItemResult> Items);

public sealed record GetOrderByIdItemResult(
    int ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal Subtotal);
