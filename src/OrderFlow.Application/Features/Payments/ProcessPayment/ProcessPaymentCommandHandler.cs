using Microsoft.Extensions.Logging;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Logging;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Application.Features.Payments.ProcessPayment;

public sealed class ProcessPaymentCommandHandler : ICommandHandler<ProcessPaymentCommand, ProcessPaymentResult>
{
    private readonly IOrderRepository _orders;
    private readonly IPaymentRepository _payments;
    private readonly IProductRepository _products;
    private readonly ITransactionRunner _transactions;
    private readonly ILogger<ProcessPaymentCommandHandler> _logger;

    public ProcessPaymentCommandHandler(
        IOrderRepository orders,
        IPaymentRepository payments,
        IProductRepository products,
        ITransactionRunner transactions,
        ILogger<ProcessPaymentCommandHandler> logger)
    {
        _orders = orders;
        _payments = payments;
        _products = products;
        _transactions = transactions;
        _logger = logger;
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

        // Without the provider's id a duplicate succeeded payment cannot be recognised (ADR-008).
        if (command.Status == PaymentAttemptStatus.Succeeded && string.IsNullOrWhiteSpace(command.ProviderTransactionId))
        {
            throw new ValidationException("Provider transaction ID is required for a succeeded payment.");
        }

        PaymentOutcome? outcome = null;

        // The payment insert and the order/product update are two SaveChanges calls; one
        // transaction makes them commit or roll back together (ADR-006).
        await _transactions.ExecuteAsync(
            async token => outcome = await RecordPaymentAsync(command, token),
            cancellationToken);

        var result = outcome!.Result;

        // Only now, after commit: a rolled-back payment must never appear in the log.
        if (outcome.NewPayment)
        {
            _logger.PaymentRecorded(
                result.PaymentAttemptId, result.OrderId, command.Amount, command.Provider.Trim(), result.PaymentAttemptStatus.ToString());
        }

        if (outcome.BecamePaid)
        {
            _logger.OrderPaid(result.OrderId);
        }

        return result;
    }

    private sealed record PaymentOutcome(ProcessPaymentResult Result, bool NewPayment, bool BecamePaid);

    private async Task<PaymentOutcome> RecordPaymentAsync(
        ProcessPaymentCommand command,
        CancellationToken cancellationToken)
    {
        // Locks the order row until commit, so payments for the same order run one at a time:
        // a twin request waits here and then sees the first one as a replay (ADR-008).
        var order = await _orders.GetByIdForUpdateAsync(command.OrderId, cancellationToken);

        if (order is null)
        {
            throw new NotFoundException("Order", command.OrderId);
        }

        if (command.ProviderTransactionId is not null)
        {
            var recorded = await _payments.GetByProviderTransactionAsync(
                command.Provider.Trim(), command.ProviderTransactionId, cancellationToken);

            if (recorded is not null)
            {
                var wasPending = order.Status == OrderStatus.PendingPayment;
                var replayed = await ReplayAsync(recorded, command, order, cancellationToken);

                return new PaymentOutcome(replayed, NewPayment: false, BecamePaid: wasPending && replayed.OrderStatus == OrderStatus.Paid);
            }
        }

        if (order.Status != OrderStatus.PendingPayment)
        {
            throw new InvalidOrderStateException(
                $"Order '{order.Id}' cannot receive a payment attempt " +
                $"from status '{order.Status}'.");
        }

        if (command.Status == PaymentAttemptStatus.Succeeded)
        {
            await EnsureWithinOrderTotalAsync(order, command.Amount, cancellationToken);
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
            await CompleteIfFullyPaidAsync(order, cancellationToken);
        }

        await _orders.UpdateAsync(order, cancellationToken);

        var result = new ProcessPaymentResult(
            order.Id,
            order.Status,
            paymentAttempt.Id,
            paymentAttempt.Status);

        return new PaymentOutcome(result, NewPayment: true, BecamePaid: order.Status == OrderStatus.Paid);
    }

    private async Task EnsureWithinOrderTotalAsync(Order order, decimal amount, CancellationToken cancellationToken)
    {
        var alreadyPaid = await _payments.GetSucceededAmountAsync(order.Id, cancellationToken);

        if (alreadyPaid + amount > order.TotalAmount)
        {
            throw new ConflictException(
                $"Payment of {amount} would exceed the order total {order.TotalAmount} " +
                $"({alreadyPaid} already paid).");
        }
    }

    private async Task CompleteIfFullyPaidAsync(Order order, CancellationToken cancellationToken)
    {
        // The new attempt is already persisted, so the repository sum includes it.
        var succeededAmount = await _payments.GetSucceededAmountAsync(order.Id, cancellationToken);

        if (succeededAmount < order.TotalAmount)
        {
            return;
        }

        var productIds = order.Items
           .Select(item => item.ProductId)
           .Distinct()
           .ToList();

        var products = await _products.GetByIdsAsync(productIds, cancellationToken);

        var productsById = products.ToDictionary(product => product.Id);

        foreach (var item in order.Items)
        {
            if (!productsById.TryGetValue(item.ProductId, out var product))
            {
                throw new NotFoundException("Product", item.ProductId);
            }

            product.FulfillReservation(item.Quantity);
        }

        order.MarkAsPaid();
    }

    // Same provider id again: identical payload is a retried callback and gets the original
    // answer; anything else reusing the id is a different payment and is refused. A recorded
    // payment can have left a fully paid order stuck in PendingPayment (the failure in INC-001),
    // so the replay finishes that work. It never adds another payment row.
    private async Task<ProcessPaymentResult> ReplayAsync(
        PaymentAttempt recorded,
        ProcessPaymentCommand command,
        Order order,
        CancellationToken cancellationToken)
    {
        var samePayment = recorded.OrderId == command.OrderId
                          && recorded.Amount == command.Amount
                          && recorded.Status == command.Status;

        if (!samePayment)
        {
            throw new ConflictException(
                $"Provider transaction '{command.ProviderTransactionId}' was already recorded with different details.");
        }

        if (recorded.Status == PaymentAttemptStatus.Succeeded && order.Status == OrderStatus.PendingPayment)
        {
            await CompleteIfFullyPaidAsync(order, cancellationToken);
            await _orders.UpdateAsync(order, cancellationToken);
        }

        return new ProcessPaymentResult(order.Id, order.Status, recorded.Id, recorded.Status);
    }
}