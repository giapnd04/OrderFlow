namespace OrderFlow.Domain.Enums;

/// <summary>
/// Payment attempt status (schema v1, §6). Only <see cref="Succeeded"/> counts toward paid_amount.
/// Persisted as nvarchar with a DB CHECK constraint.
/// </summary>
public enum PaymentAttemptStatus
{
    Pending,
    Succeeded,
    Failed,
    Cancelled,
    Expired
}
