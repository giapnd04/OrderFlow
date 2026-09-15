namespace OrderFlow.Domain.Enums;

/// <summary>
/// Order lifecycle status (ADR-003). A deliberate workflow projection of payment
/// state, not a full separation from it — <c>payment_attempt</c> remains the source
/// of truth for whether an order is paid. Valid transitions:
/// PendingPayment→Paid, PendingPayment→Cancelled, Paid→Confirmed, Confirmed→Cancelled.
/// <see cref="Paid"/> is intentionally a locked state — not directly cancellable;
/// an order must be confirmed first. Persisted as nvarchar with a DB CHECK constraint.
/// </summary>
public enum OrderStatus
{
    PendingPayment,
    Paid,
    Confirmed,
    Cancelled,
    Shipped,
    Delivered
}
