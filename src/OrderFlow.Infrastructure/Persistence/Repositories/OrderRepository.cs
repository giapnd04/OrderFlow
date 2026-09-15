using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class OrderRepository : IOrderRepository
{
    private readonly OrderFlowDbContext _db;

    public OrderRepository(OrderFlowDbContext db) => _db = db;

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default)
    {
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .AsNoTracking()
            .Include(order => order.Items)
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    public async Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == orderId,
                cancellationToken);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(OrderStatus? status, int? customerId,
    int pageNumber,
    int pageSize,
    CancellationToken cancellationToken = default)
    {
        var query = _db.Orders
            .AsNoTracking()
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(order => order.Status == status.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(order => order.CustomerId == customerId.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var orders = await query
            .OrderBy(order => order.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (orders, totalCount);
    }
}
