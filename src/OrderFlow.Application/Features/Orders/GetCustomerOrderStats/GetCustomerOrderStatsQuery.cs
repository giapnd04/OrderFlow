using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Orders.GetCustomerOrderStats;

public sealed record GetCustomerOrderStatsQuery(int CustomerId) : IQuery<GetCustomerOrderStatsResult>;
