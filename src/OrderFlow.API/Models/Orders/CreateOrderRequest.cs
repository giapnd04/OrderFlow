namespace OrderFlow.API.Models.Orders;

public sealed record CreateOrderRequest(int CustomerId, IReadOnlyList<CreateOrderItemRequest> Items);

public sealed record CreateOrderItemRequest(int ProductId, int Quantity);
