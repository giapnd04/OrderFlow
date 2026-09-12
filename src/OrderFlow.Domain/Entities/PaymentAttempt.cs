using OrderFlow.Domain.Common;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Domain.Entities;

/// <summary>
/// A single attempt to pay an <see cref="Order"/> through a payment provider (schema v1, §5).
/// An order may have many attempts; only <see cref="PaymentAttemptStatus.Succeeded"/> ones
/// count toward paid_amount. (provider, provider_transaction_id) is unique when the
/// transaction id is present — a DB-level idempotency guard for provider webhooks.
/// </summary>
public class PaymentAttempt : AuditableEntity
{
    public int OrderId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentAttemptStatus Status { get; private set; }

    public string Provider { get; private set; } = null!;

    public string? ProviderTransactionId { get; private set; }

    public static PaymentAttempt Create(
      int orderId,
      decimal amount,
      string provider,
      PaymentAttemptStatus status,
      string? providerTransactionId = null)
    {
        if (orderId <= 0)
        {
            throw new DomainException("Order ID must be greater than zero.");
        }

        if (amount <= 0)
        {
            throw new DomainException("Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainException("Payment provider is required.");
        }

        return new PaymentAttempt
        {
            OrderId = orderId,
            Amount = amount,
            Provider = provider.Trim(),
            Status = status,
            ProviderTransactionId = providerTransactionId
        };
    }
}
