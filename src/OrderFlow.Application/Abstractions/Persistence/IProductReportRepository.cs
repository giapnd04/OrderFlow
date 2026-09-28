using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Abstractions.Persistence;

/// <summary>
/// Read-only reporting queries over products, kept apart from <see cref="IProductRepository"/>
/// (the write/aggregate side).
/// </summary>
public interface IProductReportRepository
{
    /// <summary>
    /// Active products whose available stock (stock_quantity - reserved_quantity) is at or
    /// below <paramref name="threshold"/>, lowest available first.
    /// </summary>
    Task<IReadOnlyList<Product>> GetLowStockAsync(
        int threshold,
        int limit,
        CancellationToken cancellationToken = default);
}
