namespace OrderFlow.Application.Features.Orders.CreateOrder;

/// <summary>
/// Outcome of a successful <see cref="CreateOrderCommand"/>. A flat contract the API
/// can return directly without exposing the <c>Order</c> domain entity (SDS §11).
/// </summary>
public sealed record CreateOrderResult(int OrderId, decimal TotalAmount, string Status);
