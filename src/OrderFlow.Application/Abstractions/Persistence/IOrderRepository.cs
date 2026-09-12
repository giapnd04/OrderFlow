using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Persistence capability for the <see cref="Order"/> aggregate.
/// </summary>
public interface IOrderRepository
{
    /// <summary>
    /// Persists a new order together with its items as a single unit of work.
    /// CreateOrder writes exactly one aggregate, so there is no separate
    /// SaveChanges / IUnitOfWork seam yet (SDS §13 — introduce one only when a
    /// use case writes multiple aggregates atomically).
    /// </summary>
    Task AddAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a single order together with its items, or <c>null</c> if no order with
    /// <paramref name="orderId"/> exists. Read-only — callers must not mutate and save
    /// the returned aggregate through this method.
    /// </summary>
    Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default);
}
