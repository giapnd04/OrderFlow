using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Payments.GetOrderPayments;

public sealed class GetOrderPaymentsQueryHandler
    : IQueryHandler<GetOrderPaymentsQuery, GetOrderPaymentsResult>
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;

    public GetOrderPaymentsQueryHandler(IOrderRepository orders, IPaymentRepository payments)
    {
        _orders = orders;
        _payments = payments;
    }

    public async Task<GetOrderPaymentsResult> Handle(
        GetOrderPaymentsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.OrderId <= 0)
        {
            throw new ValidationException("OrderId must be greater than zero.");
        }

        var order = await _orders.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", query.OrderId);
        }

        var attempts = await _payments.GetByOrderIdAsync(order.Id, cancellationToken);

        var items = attempts
            .Select(attempt => new GetOrderPaymentsItemResult(
                attempt.Id,
                attempt.Amount,
                attempt.Status.ToString(),
                attempt.Provider,
                attempt.ProviderTransactionId))
            .ToList();

        return new GetOrderPaymentsResult(order.Id, items);
    }
}
