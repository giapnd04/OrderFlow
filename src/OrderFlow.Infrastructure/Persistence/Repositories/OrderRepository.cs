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

    // UPDLOCK on the order row: inside a transaction (ProcessPayment) it makes every change to
    // the same order queue behind the first one until commit (ADR-008). Outside a transaction
    // the lock ends with this SELECT, so other callers see no difference. The hint sits on the
    // statement that filters by id, so only this one row is locked.
    public async Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default)
    {
        return await _db.Orders
            .FromSqlInterpolated($"SELECT * FROM orders WITH (UPDLOCK, ROWLOCK) WHERE id = {orderId}")
            .Include(order => order.Items)
            .FirstOrDefaultAsync(
                order => order.Id == orderId,
                cancellationToken);
    }

    public async Task UpdateAsync(Order order, CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
    }
}
