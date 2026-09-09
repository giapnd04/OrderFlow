using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Sellable product with its own stock level (schema v1, §2). sku is unique.
/// stock_quantity is decremented atomically at payment success; row_version guards
/// concurrent updates outside that atomic path.
/// </summary>
public class Product : AuditableEntity
{
    public string Sku { get; set; } = null!;

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public int StockQuantity { get; set; }

    public ProductStatus Status { get; set; }

    public byte[] RowVersion { get; set; } = [];
}
