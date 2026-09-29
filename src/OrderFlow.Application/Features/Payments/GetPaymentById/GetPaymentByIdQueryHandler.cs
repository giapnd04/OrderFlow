using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Payments.GetPaymentById;

public sealed class GetPaymentByIdQueryHandler
    : IQueryHandler<GetPaymentByIdQuery, GetPaymentByIdResult>
{
    private readonly IPaymentRepository _payments;

    public GetPaymentByIdQueryHandler(IPaymentRepository payments)
    {
        _payments = payments;
    }

    public async Task<GetPaymentByIdResult> Handle(
        GetPaymentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var payment = await _payments.GetByIdAsync(query.PaymentAttemptId, cancellationToken);

        if (payment is null)
        {
            throw new NotFoundException("PaymentAttempt", query.PaymentAttemptId);
        }

        return new GetPaymentByIdResult(
            payment.Id,
            payment.OrderId,
            payment.Amount,
            payment.Status.ToString(),
            payment.Provider,
            payment.ProviderTransactionId);
    }
}
