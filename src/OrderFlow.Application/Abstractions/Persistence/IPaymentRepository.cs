using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task AddAsync(PaymentAttempt paymentAttempt, CancellationToken cancellationToken = default);

    /// <summary>The attempt already recorded for this provider id, if any (replay detection, ADR-008).</summary>
    Task<PaymentAttempt?> GetByProviderTransactionAsync(
        string provider,
        string providerTransactionId,
        CancellationToken cancellationToken = default);

    Task<PaymentAttempt?> GetByIdAsync(int paymentAttemptId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PaymentAttempt>> GetByOrderIdAsync(int orderId, CancellationToken cancellationToken = default);

    Task<(IReadOnlyCollection<PaymentAttempt> Payments, int TotalCount)> GetPagedAsync(
        PaymentAttemptStatus? status,
        string? provider,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<decimal> GetSucceededAmountAsync(int orderId, CancellationToken cancellationToken = default);
}