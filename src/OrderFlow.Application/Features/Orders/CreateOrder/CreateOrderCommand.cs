using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.CreateOrder;

/// <summary>
/// Request to record a new order for a customer (SRS §11, §32 — Order: Create orders).
/// This is the application-facing contract; the API request DTO maps onto it at the
/// boundary (SDS §11).
/// </summary>
public sealed record CreateOrderCommand(
    int CustomerId,
    IReadOnlyList<CreateOrderItemInput> Items) : ICommand<CreateOrderResult>;

/// <summary>
/// One requested product line. Price is intentionally NOT accepted from the caller —
/// it is snapshotted server-side from the current product price (schema v1, §4).
/// </summary>
public sealed record CreateOrderItemInput(int ProductId, int Quantity);
