namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Persistence capability the order use cases need for customers. Kept to the
/// operations an actual use case requires (SDS §8) — CreateOrder only needs to
/// confirm the customer exists.
/// </summary>
public interface ICustomerRepository
{
    Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default);

    Task<OrderFlow.Domain.Entities.Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default);

    Task<OrderFlow.Domain.Entities.Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<OrderFlow.Domain.Entities.Customer> Customers, int TotalCount)> GetPagedAsync(
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> AddAsync(OrderFlow.Domain.Entities.Customer customer, CancellationToken cancellationToken = default);

    Task UpdateAsync(OrderFlow.Domain.Entities.Customer customer, CancellationToken cancellationToken = default);
}
