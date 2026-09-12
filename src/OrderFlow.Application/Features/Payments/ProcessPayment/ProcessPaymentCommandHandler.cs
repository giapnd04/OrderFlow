using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Application.Features.Payments.ProcessPayment;

public sealed class ProcessPaymentCommandHandler : ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;

    public ProcessPaymentCommandHandler(
        IOrderRepository orders,
        IPaymentRepository payments)
    {
        _orders = orders;
        _payments = payments;
    }

    public async Task<ProcessPaymentResult> Handle(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.OrderId <= 0)
        {
            throw new ValidationException("Order ID must be greater than zero.");
        }

        if (command.Amount <= 0)
        {
            throw new ValidationException("Payment amount must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(command.Provider))
        {
            throw new ValidationException("Payment provider is required.");
        }

        var order = await _orders.GetByIdForUpdateAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", command.OrderId);
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOrderStateException(
                $"Order '{order.Id}' cannot receive a payment attempt " +
                $"from status '{order.Status}'.");
        }

        var paymentAttempt = PaymentAttempt.Create(
            order.Id,
            command.Amount,
            command.Provider,
            command.Status,
            command.ProviderTransactionId);

        await _payments.AddAsync(paymentAttempt, cancellationToken);

        if (command.Status == PaymentAttemptStatus.Succeeded)
        {
            var succeededAmount = await _payments.GetSucceededAmountAsync(order.Id, cancellationToken);

            succeededAmount += command.Amount;

            if (succeededAmount >= order.TotalAmount)
            {
                order.MarkAsPaid();
            }
        }

        await _orders.UpdateAsync(order, cancellationToken);

        return new ProcessPaymentResult(
            order.Id,
            order.Status,
            paymentAttempt.Id,
            paymentAttempt.Status);
    }
}