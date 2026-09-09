using OrderFlow.Domain.Common;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Line item within an <see cref="Order"/> (schema v1, §4). A product appears at most
/// once per order (UNIQUE(order_id, product_id)); repeat purchases increase quantity.
/// </summary>
public class OrderItem : Entity
{
    public int OrderId { get; set; }

    public int ProductId { get; set; }

    public int Quantity { get; set; }

    /// <summary>Price snapshot captured when the order was created; independent of the current product price.</summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Persisted computed column: quantity * unit_price. Maintained by the database so it
    /// can never drift from the application's own calculation (schema v1, §4).
    /// </summary>
    public decimal Subtotal { get; private set; }
}
