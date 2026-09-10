using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Order aggregate root (schema v1, §3). Owns its <see cref="Items"/>.
/// total_amount is the amount the order must be paid; it is recomputed here in the
/// same operation as any item mutation (schema v1, §10).
/// Payment state is NOT stored here — it is derived from succeeded payment_attempts.
/// </summary>
public class Order : AuditableEntity
{
    private readonly List<OrderItem> _items = new();

    // EF Core materialisation constructor.
    private Order()
    {
    }

    public int CustomerId { get; private set; }

    public decimal TotalAmount { get; private set; }

    public OrderStatus Status { get; private set; }

    public byte[] RowVersion { get; private set; } = [];

    public IReadOnlyCollection<OrderItem> Items => _items;

    /// <summary>
    /// Creates a new order for <paramref name="customerId"/> from <paramref name="items"/>.
    /// Invariants enforced: at least one item, a product may appear only once
    /// (schema v1, §4 — UNIQUE(order_id, product_id)), and total_amount always equals
    /// the sum of the line subtotals. A new order starts in
    /// <see cref="OrderStatus.PendingPayment"/>; payment is handled by a separate flow.
    /// </summary>
    public static Order Create(int customerId, IEnumerable<OrderItem> items)
    {
        if (customerId <= 0)
        {
            throw new DomainException("Order requires a valid customer.");
        }

        var lines = items?.ToList() ?? new List<OrderItem>();

        if (lines.Count == 0)
        {
            throw new DomainException("Order requires at least one item.");
        }

        if (lines.Select(i => i.ProductId).Distinct().Count() != lines.Count)
        {
            throw new DomainException("An order cannot contain the same product more than once.");
        }

        var order = new Order
        {
            CustomerId = customerId,
            Status = OrderStatus.PendingPayment,
        };

        order._items.AddRange(lines);
        order.TotalAmount = lines.Sum(i => i.Quantity * i.UnitPrice);

        return order;
    }
}
