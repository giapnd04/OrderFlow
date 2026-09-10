namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Persistence capability the order use cases need for customers. Kept to the
/// operations an actual use case requires (SDS §8) — CreateOrder only needs to
/// confirm the customer exists.
/// </summary>
public interface ICustomerRepository
{
    Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default);
}
