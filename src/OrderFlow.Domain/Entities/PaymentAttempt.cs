using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// A single attempt to pay an <see cref="Order"/> through a payment provider (schema v1, §5).
/// An order may have many attempts; only <see cref="PaymentAttemptStatus.Succeeded"/> ones
/// count toward paid_amount. (provider, provider_transaction_id) is unique when the
/// transaction id is present — a DB-level idempotency guard for provider webhooks.
/// </summary>
public class PaymentAttempt : AuditableEntity
{
    public int OrderId { get; set; }

    public decimal Amount { get; set; }

    public PaymentAttemptStatus Status { get; set; }

    public string Provider { get; set; } = null!;

    public string? ProviderTransactionId { get; set; }
}
