using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.CreateOrder;


public sealed record CreateOrderCommand(int CustomerId, IReadOnlyList<CreateOrderItemInput> Items) : ICommand<CreateOrderResult>;

public sealed record CreateOrderItemInput(int ProductId, int Quantity);
