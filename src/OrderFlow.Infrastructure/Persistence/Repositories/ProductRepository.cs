using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class ProductRepository : IProductRepository
{
    private readonly OrderFlowDbContext _db;

    public ProductRepository(OrderFlowDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetByIdsAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken = default)
    {
        if (productIds.Count == 0)
        {
            return Array.Empty<Product>();
        }

        return await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsBySkuAsync(
     string sku,
     CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .AnyAsync(p => p.Sku == sku, cancellationToken);
    }

    public async Task AddAsync(Product product, CancellationToken cancellationToken = default)
    {
        _db.Products.Add(product);

        await _db.SaveChangesAsync(cancellationToken);
    }
}
