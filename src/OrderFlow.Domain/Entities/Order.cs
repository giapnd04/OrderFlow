using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// Order aggregate root (schema v1, §3). Owns its <see cref="Items"/>.
/// total_amount is the amount the order must be paid; it is recomputed by the
/// application/domain in the same transaction as any item mutation (schema v1, §10).
/// Payment state is NOT stored here — it is derived from succeeded payment_attempts.
/// </summary>
public class Order : AuditableEntity
{
    public int CustomerId { get; set; }

    public decimal TotalAmount { get; set; }

    public OrderStatus Status { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
