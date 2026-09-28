using Microsoft.EntityFrameworkCore;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Infrastructure.Persistence.Repositories;

internal sealed class ProductReportRepository : IProductReportRepository
{
    private readonly OrderFlowDbContext _db;

    public ProductReportRepository(OrderFlowDbContext db) => _db = db;

    public async Task<IReadOnlyList<Product>> GetLowStockAsync(
        int threshold,
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await _db.Products
            .AsNoTracking()
            .Where(p => p.Status == ProductStatus.Active
                && p.StockQuantity - p.ReservedQuantity <= threshold)
            .OrderBy(p => p.StockQuantity - p.ReservedQuantity)
            .ThenBy(p => p.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
