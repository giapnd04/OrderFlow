using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Persistence capability the order use cases need for products. CreateOrder loads
/// the ordered products in one round trip to validate them and to snapshot their
/// current price onto the order lines.
/// </summary>
public interface IProductRepository
{
    Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken = default);
}
