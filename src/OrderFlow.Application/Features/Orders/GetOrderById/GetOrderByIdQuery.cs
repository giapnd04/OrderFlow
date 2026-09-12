using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.GetOrderById;

public sealed record GetOrderByIdQuery(int OrderId) : IQuery<GetOrderByIdResult>;
