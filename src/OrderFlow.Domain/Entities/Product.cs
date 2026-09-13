using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Sellable product with its own stock level (schema v1, §2). sku is unique.
/// stock_quantity is decremented atomically at payment success; row_version guards
/// concurrent updates outside that atomic path.
/// </summary>
public class Product : AuditableEntity
{
    public string Sku { get; private set; } = null!;

    public string Name { get; private set; } = null!;

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public ProductStatus Status { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    // EF Core materialisation constructor.
    private Product()
    {
    }

    public static Product Create(
        string sku,
        string name,
        decimal price,
        int stockQuantity,
        ProductStatus status = ProductStatus.Active)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new DomainException("Product requires a SKU.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product requires a name.");
        }

        if (price < 0)
        {
            throw new DomainException("Product price cannot be negative.");
        }

        if (stockQuantity < 0)
        {
            throw new DomainException("Product stock quantity cannot be negative.");
        }

        return new Product
        {
            Sku = sku.Trim(),
            Name = name.Trim(),
            Price = price,
            StockQuantity = stockQuantity,
            Status = status,
        };
    }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new DomainException(
                "Quantity to decrease must be greater than zero.");
        }

        if (quantity > StockQuantity)
        {
            throw new InsufficientStockException(
                $"Insufficient stock for product '{Sku}'.");
        }

        StockQuantity -= quantity;
    }
}
