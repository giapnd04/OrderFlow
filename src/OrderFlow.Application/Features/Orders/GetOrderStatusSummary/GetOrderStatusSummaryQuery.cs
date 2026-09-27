using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.GetOrderStatusSummary;

public sealed record GetOrderStatusSummaryQuery : IQuery<GetOrderStatusSummaryResult>;
