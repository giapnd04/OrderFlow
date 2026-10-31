using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

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

    public async Task<IReadOnlyList<Product>> GetByIdsWithUpdateLockAsync(
        IReadOnlyCollection<int> productIds,
        CancellationToken cancellationToken = default)
    {
        // Ascending id order: two orders sharing products lock them in the same sequence,
        // so neither can hold one row while waiting for the other (no deadlock).
        var ids = productIds.Distinct().Order().ToArray();

        if (ids.Length == 0)
        {
            return Array.Empty<Product>();
        }

        // The hint must sit on the statement that filters by id. Composing LINQ on top
        // (FromSql(...).Where(...)) would wrap it in a subquery and lock every row scanned.
        var placeholders = string.Join(", ", ids.Select((_, i) => $"{{{i}}}"));

        return await _db.Products
            .FromSqlRaw(
                $"SELECT * FROM products WITH (UPDLOCK, ROWLOCK) WHERE id IN ({placeholders})",
                ids.Cast<object>().ToArray())
            .ToListAsync(cancellationToken);
    }

    public async Task<Product?> GetByIdAsync(
        int productId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
    }

    public Task<Product?> GetByIdForUpdateAsync(
        int productId,
        CancellationToken cancellationToken = default)
        => _db.Products.FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

    public async Task<(IReadOnlyCollection<Product> Products, int TotalCount)> GetPagedAsync(
        string? search,
        ProductStatus? status,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{search}%") ||
                EF.Functions.Like(p.Sku, $"%{search}%"));
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var products = await query
            .OrderBy(p => p.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (products, totalCount);
    }

    public async Task UpdateAsync(Product product, CancellationToken cancellationToken = default)
    {
        await _db.SaveChangesAsync(cancellationToken);
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
