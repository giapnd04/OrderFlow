using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.ShipOrder;
public sealed record ShipOrderCommand(int OrderId) : ICommand<ShipOrderResult>;
