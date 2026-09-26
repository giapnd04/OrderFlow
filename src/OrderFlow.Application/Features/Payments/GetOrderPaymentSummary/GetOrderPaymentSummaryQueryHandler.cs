using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Payments.GetOrderPaymentSummary;

public sealed class GetOrderPaymentSummaryQueryHandler
    : IQueryHandler<GetOrderPaymentSummaryQuery, GetOrderPaymentSummaryResult>
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;

    public GetOrderPaymentSummaryQueryHandler(IOrderRepository orders, IPaymentRepository payments)
    {
        _orders = orders;
        _payments = payments;
    }

    public async Task<GetOrderPaymentSummaryResult> Handle(
        GetOrderPaymentSummaryQuery query,
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

        var paidAmount = await _payments.GetSucceededAmountAsync(order.Id, cancellationToken);

        return new GetOrderPaymentSummaryResult(
            order.Id,
            order.Status.ToString(),
            order.TotalAmount,
            paidAmount,
            Math.Max(0m, order.TotalAmount - paidAmount),
            paidAmount >= order.TotalAmount);
    }
}
