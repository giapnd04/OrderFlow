using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Orders.GetOrders;

public sealed record GetOrdersQuery(int PageNumber = 1, int PageSize = 20, OrderStatus? Status = null, int? CustomerId = null)
    : IQuery<GetOrdersResult>;