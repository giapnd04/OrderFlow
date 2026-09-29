using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task AddAsync(PaymentAttempt paymentAttempt, CancellationToken cancellationToken = default);

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