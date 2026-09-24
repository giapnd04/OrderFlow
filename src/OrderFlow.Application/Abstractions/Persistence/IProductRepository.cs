using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

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

    Task<Product?> GetByIdAsync(int productId, CancellationToken cancellationToken = default);

    Task<Product?> GetByIdForUpdateAsync(int productId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<Product> Products, int TotalCount)> GetPagedAsync(
        string? search,
        ProductStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(Product product, CancellationToken cancellationToken = default);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task AddAsync(Product product, CancellationToken cancellationToken = default);

}
