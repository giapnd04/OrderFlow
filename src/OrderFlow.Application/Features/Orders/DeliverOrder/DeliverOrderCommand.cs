using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.DeliverOrder;
public sealed record DeliverOrderCommand(int OrderId) : ICommand<DeliverOrderResult>;
