using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Abstractions.Persistence;

public interface IPaymentRepository
{
    Task AddAsync(PaymentAttempt paymentAttempt, CancellationToken cancellationToken = default);

    Task<decimal> GetSucceededAmountAsync(int orderId, CancellationToken cancellationToken = default);
}