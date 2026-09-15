using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Orders.GetOrders;

public sealed class GetOrdersQueryHandler
    : IQueryHandler<GetOrdersQuery, GetOrdersResult>
{
    private const int MaxPageSize = 100;

    private readonly IOrderRepository _orders;

    public GetOrdersQueryHandler(IOrderRepository orders)
    {
        _orders = orders;
    }

    public async Task<GetOrdersResult> Handle(
        GetOrdersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.PageNumber <= 0)
        {
            throw new ValidationException("PageNumber must be greater than zero.");
        }

        if (query.PageSize <= 0)
        {
            throw new ValidationException("PageSize must be greater than zero.");
        }

        if (query.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot be greater than {MaxPageSize}.");
        }

        var (orders, totalCount) = await _orders.GetPagedAsync(
            query.Status,
            query.CustomerId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / query.PageSize);

        var items = orders
            .Select(order => new GetOrdersItemResult(
                order.Id,
                order.CustomerId,
                order.Status.ToString(),
                order.TotalAmount))
            .ToList();

        return new GetOrdersResult(items, query.PageNumber, query.PageSize, totalCount, totalPages);
    }
}