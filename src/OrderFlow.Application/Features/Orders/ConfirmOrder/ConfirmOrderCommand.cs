using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.ConfirmOrder;
public sealed record ConfirmOrderCommand(int OrderId) : ICommand<ConfirmOrderResult>;
