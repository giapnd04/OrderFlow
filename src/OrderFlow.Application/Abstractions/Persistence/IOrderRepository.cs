using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Persistence capability for the <see cref="Order"/> aggregate.
/// </summary>
public interface IOrderRepository
{
    Task AddAsync(Order order, CancellationToken cancellationToken = default);
    Task<Order?> GetByIdAsync(int orderId, CancellationToken cancellationToken = default);

    Task<Order?> GetByIdForUpdateAsync(int orderId, CancellationToken cancellationToken = default);

    Task UpdateAsync(Order order, CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<Order> Orders, int TotalCount)> GetPagedAsync(OrderStatus? status, int? customerId, int pageNumber, int pageSize,
    CancellationToken cancellationToken = default);
}
