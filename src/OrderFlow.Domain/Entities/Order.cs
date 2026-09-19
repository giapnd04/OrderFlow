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

    /// <summary>
    /// Cancels the order. Valid from <see cref="OrderStatus.PendingPayment"/> (before
    /// payment) or <see cref="OrderStatus.Confirmed"/> (after Sales review). Not valid
    /// from <see cref="OrderStatus.Paid"/> — a paid order is locked until a Sales
    /// action (<see cref="Confirm"/>) reviews it (ADR-003).
    /// </summary>
    public void Cancel()
    {
        if (Status != OrderStatus.PendingPayment && Status != OrderStatus.Confirmed)
        {
            throw new InvalidOrderStateException(
                $"Order cannot be cancelled from status '{Status}'.");
        }

        Status = OrderStatus.Cancelled;
    }

    public void MarkAsPaid()
    {
        if (Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOrderStateException(
                $"Order '{Id}' cannot be marked as paid from status '{Status}'.");
        }

        Status = OrderStatus.Paid;
    }

    /// <summary>
    /// Sales confirms a paid order, unlocking it for cancellation again (ADR-003).
    /// Valid only from <see cref="OrderStatus.Paid"/>.
    /// </summary>
    public void Confirm()
    {
        if (Status != OrderStatus.Paid)
        {
            throw new InvalidOrderStateException(
                $"Order '{Id}' cannot be confirmed from status '{Status}'.");
        }

        Status = OrderStatus.Confirmed;
    }

    /// <summary>
    /// Marks a confirmed order as shipped. Valid only from
    /// <see cref="OrderStatus.Confirmed"/> — a fulfillment fact, not reachable
    /// before Sales review. <see cref="OrderStatus.Shipped"/> is a terminal-ish
    /// status for v1: no <c>Cancel</c> path exists from it (no return/refund
    /// mechanism yet), matching the ADR-004 stance that reversing stock once it
    /// has left the warehouse is out of scope.
    /// </summary>
    public void Ship()
    {
        if (Status != OrderStatus.Confirmed)
        {
            throw new InvalidOrderStateException(
                $"Order '{Id}' cannot be shipped from status '{Status}'.");
        }

        Status = OrderStatus.Shipped;
    }

    /// <summary>
    /// Marks a shipped order as delivered. Valid only from
    /// <see cref="OrderStatus.Shipped"/>. Terminal status for v1 — no further
    /// transition exists out of <see cref="OrderStatus.Delivered"/> (no
    /// return/refund mechanism yet, same as <see cref="Ship"/>).
    /// </summary>
    public void Deliver()
    {
        if (Status != OrderStatus.Shipped)
        {
            throw new InvalidOrderStateException(
                $"Order '{Id}' cannot be delivered from status '{Status}'.");
        }

        Status = OrderStatus.Delivered;
    }
}
