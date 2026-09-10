using OrderFlow.Domain.Common;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Line item within an <see cref="Order"/> (schema v1, §4). A product appears at most
/// once per order (UNIQUE(order_id, product_id)); repeat purchases increase quantity.
/// </summary>
public class OrderItem : Entity
{
    // EF Core materialisation constructor.
    private OrderItem()
    {
    }

    public int OrderId { get; private set; }

    public int ProductId { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Price snapshot captured when the order was created; independent of the current product price.</summary>
    public decimal UnitPrice { get; private set; }

    /// <summary>
    /// Persisted computed column: quantity * unit_price. Maintained by the database so it
    /// can never drift from the application's own calculation (schema v1, §4).
    /// </summary>
    public decimal Subtotal { get; private set; }

    /// <summary>
    /// Creates a line for <paramref name="productId"/>, capturing <paramref name="unitPrice"/>
    /// as an immutable snapshot. <paramref name="quantity"/> must be positive and
    /// <paramref name="unitPrice"/> non-negative — the same guarantees the database CHECK
    /// constraints enforce (schema v1, §4).
    /// </summary>
    public static OrderItem Create(int productId, int quantity, decimal unitPrice)
    {
        if (productId <= 0)
        {
            throw new DomainException("Order item requires a valid product.");
        }

        if (quantity <= 0)
        {
            throw new DomainException("Order item quantity must be greater than zero.");
        }

        if (unitPrice < 0)
        {
            throw new DomainException("Order item unit price cannot be negative.");
        }

        return new OrderItem
        {
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
        };
    }
}
