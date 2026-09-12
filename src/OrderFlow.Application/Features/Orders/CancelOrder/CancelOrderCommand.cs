using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.CancelOrder;
public sealed record CancelOrderCommand(int OrderId) : ICommand<CancelOrderResult>;