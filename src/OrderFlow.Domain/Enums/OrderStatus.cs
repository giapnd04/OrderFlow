namespace OrderFlow.Domain.Enums;

/// <summary>
/// Order lifecycle status (schema v1, §6). Distinct from payment status.
/// Persisted as nvarchar with a DB CHECK constraint.
/// </summary>
public enum OrderStatus
{
    PendingPayment,
    Paid,
    Processing,
    Completed,
    Cancelled
}
