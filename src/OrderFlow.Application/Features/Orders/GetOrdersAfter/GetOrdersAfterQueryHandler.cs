using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.GetOrders;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Orders.GetOrdersAfter;

public sealed record GetOrdersAfterQuery(int? AfterId, int PageSize = 20, OrderStatus? Status = null, int? CustomerId = null)
    : IQuery<GetOrdersAfterResult>;

/// <summary>NextAfterId is the cursor for the next page; null means there is no next page.</summary>
public sealed record GetOrdersAfterResult(IReadOnlyCollection<GetOrdersItemResult> Items, int? NextAfterId);

/// <summary>
/// Keyset ("cursor") paging over orders (OF-09). The client passes the last id it saw instead of a
/// page number, so every page costs one seek, however deep, and there is no COUNT(*).
/// </summary>
public sealed class GetOrdersAfterQueryHandler : IQueryHandler<GetOrdersAfterQuery, GetOrdersAfterResult>
{
    private const int MaxPageSize = 100;

    private readonly IOrderQueries _orders;

    public GetOrdersAfterQueryHandler(IOrderQueries orders) => _orders = orders;

    public async Task<GetOrdersAfterResult> Handle(GetOrdersAfterQuery query, CancellationToken cancellationToken = default)
    {
        if (query.PageSize is <= 0 or > MaxPageSize)
        {
            throw new ValidationException($"PageSize must be between 1 and {MaxPageSize}.");
        }

        if (query.AfterId < 0)
        {
            throw new ValidationException("AfterId cannot be negative.");
        }

        // One extra row tells whether another page exists, without a second query.
        var rows = await _orders.GetPageAfterAsync(
            query.Status, query.CustomerId, query.AfterId, query.PageSize + 1, cancellationToken);

        var page = rows.Take(query.PageSize)
            .Select(order => new GetOrdersItemResult(order.OrderId, order.CustomerId, order.Status.ToString(), order.TotalAmount))
            .ToList();

        var nextAfterId = rows.Count > query.PageSize ? page[^1].OrderId : (int?)null;

        return new GetOrdersAfterResult(page, nextAfterId);
    }
}
