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
    public int ReservedQuantity { get; private set; }



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

    /// <summary>
    /// Updates name and price. SKU and stock are not editable here: SKU is the unique
    /// identity, and stock changes go through Reserve/Fulfill/Restock. Existing order
    /// lines keep their snapshot unit price, so a price change is not retroactive.
    /// </summary>
    public void UpdateDetails(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Product requires a name.");
        }

        if (price < 0)
        {
            throw new DomainException("Product price cannot be negative.");
        }

        Name = name.Trim();
        Price = price;
    }

    //public void DecreaseStock(int quantity)
    //{
    //    if (quantity <= 0)
    //    {
    //        throw new DomainException("Quantity to decrease must be greater than zero.");
    //    }

    //    if (quantity > StockQuantity)
    //    {
    //        throw new InsufficientStockException($"Insufficient stock for product '{Sku}'.");
    //    }

    //    StockQuantity -= quantity;
    //}

    public void Reserve(int quantity)
    {

        ValidateQuantity(quantity, "reserve");

        var availableQuantity = StockQuantity - ReservedQuantity;

        if (quantity > availableQuantity)
        {
            throw new InsufficientStockException($"Insufficient available stock for product '{Sku}'.");
        }

        ReservedQuantity += quantity;
    }

    public void ReleaseReservation(int quantity)
    {
        ValidateQuantity(quantity, "release");

        if (quantity > ReservedQuantity)
        {
            throw new DomainException($"Cannot release more reservation than currently reserved for product '{Sku}'.");
        }

        ReservedQuantity -= quantity;
    }

    public void FulfillReservation(int quantity)
    {
        ValidateQuantity(quantity, "fulfill");

        if (quantity > ReservedQuantity)
        {
            throw new DomainException($"Cannot fulfill more reservation than currently reserved for product '{Sku}'.");
        }

        StockQuantity -= quantity;
        ReservedQuantity -= quantity;
    }

    /// <summary>
    /// Stops the product from being ordered (CreateOrder rejects non-Active products).
    /// Idempotent. Existing reservations and orders are untouched.
    /// </summary>
    public void Discontinue()
    {
        Status = ProductStatus.Discontinued;
    }

    public void Restock(int quantity)
    {
        ValidateQuantity(quantity, "restock");

        StockQuantity += quantity;
    }

    private static void ValidateQuantity(int quantity, string operation)
    {
        if (quantity <= 0)
            throw new DomainException(
                $"Quantity to {operation} must be greater than zero.");
    }
}
