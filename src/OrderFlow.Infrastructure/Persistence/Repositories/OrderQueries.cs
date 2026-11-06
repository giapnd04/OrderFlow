using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class OrderQueries : IOrderQueries
{
    private readonly OrderFlowDbContext _db;

    public OrderQueries(OrderFlowDbContext db) => _db = db;

    public async Task<(IReadOnlyList<OrderListItem> Items, int TotalCount)> GetPageAsync(
        OrderStatus? status,
        int? customerId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = Filtered(status, customerId);

        var totalCount = await query.CountAsync(cancellationToken);

        // OFFSET still reads and throws away every row before the page, so deep pages cost more.
        var items = await Project(query
                .OrderBy(order => order.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<OrderListItem>> GetPageAfterAsync(
        OrderStatus? status,
        int? customerId,
        int? afterId,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = Filtered(status, customerId);

        if (afterId is { } lastSeen)
        {
            // Seeks straight to the first id after the cursor on the clustered index.
            query = query.Where(order => order.Id > lastSeen);
        }

        return await Project(query.OrderBy(order => order.Id).Take(take))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Order> Filtered(OrderStatus? status, int? customerId)
    {
        var query = _db.Orders.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(order => order.Status == status.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(order => order.CustomerId == customerId.Value);
        }

        return query;
    }

    // Selecting into the row type makes EF fetch only these four columns, not row_version,
    // timestamps or anything else on the entity.
    private static IQueryable<OrderListItem> Project(IQueryable<Order> query)
        => query.Select(order => new OrderListItem(order.Id, order.CustomerId, order.Status, order.TotalAmount));
}
