using Microsoft.Extensions.DependencyInjection;
using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Payments;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Payments.ProcessPayment;
using OrderFlow.Domain.Enums;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.Application.Features.Payments.ReconcilePayments;

/// <summary>
/// Compares what the payment provider collected with what OrderFlow recorded (OF-06). Each
/// captured charge goes through <see cref="ProcessPaymentCommandHandler"/>, so reconciliation
/// has no second way of writing money: the order lock, replay and over-payment rules all apply,
/// and running it again changes nothing. What it must not decide alone (money taken for a
/// cancelled order, a differing amount) is reported for a person to review.
/// </summary>
public sealed class ReconcilePaymentsCommandHandler
    : ICommandHandler<ReconcilePaymentsCommand, ReconcilePaymentsResult>
{
    private readonly IPaymentProviderLedger _ledger;
    private readonly IServiceScopeFactory _scopes;

    public ReconcilePaymentsCommandHandler(IPaymentProviderLedger ledger, IServiceScopeFactory scopes)
    {
        _ledger = ledger;
        _scopes = scopes;
    }

    public async Task<ReconcilePaymentsResult> Handle(
        ReconcilePaymentsCommand command,
        CancellationToken cancellationToken = default)
    {
        var captured = await _ledger.GetCapturedTransactionsAsync(cancellationToken);

        var paymentsRecorded = 0;
        var ordersCompleted = 0;
        var unchanged = 0;
        var needsReview = new List<ReconciliationIssue>();

        foreach (var transaction in captured)
        {
            try
            {
                var outcome = await ReconcileOneAsync(transaction, cancellationToken);

                paymentsRecorded += outcome.PaymentRecorded ? 1 : 0;
                ordersCompleted += outcome.OrderCompleted ? 1 : 0;
                unchanged += outcome.PaymentRecorded || outcome.OrderCompleted ? 0 : 1;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // One bad charge must not stop the rest of the batch; its transaction has rolled back.
                needsReview.Add(new ReconciliationIssue(
                    transaction.Provider,
                    transaction.ProviderTransactionId,
                    transaction.OrderId,
                    transaction.Amount,
                    ReasonFor(ex)));
            }
        }

        return new ReconcilePaymentsResult(captured.Count, paymentsRecorded, ordersCompleted, unchanged, needsReview);
    }

    // A scope per charge gives each one its own DbContext, so entities left behind by a
    // failed charge can never be saved by the next one.
    private async Task<(bool PaymentRecorded, bool OrderCompleted)> ReconcileOneAsync(
        CapturedTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var alreadyRecorded = await services.GetRequiredService<IPaymentRepository>()
            .GetByProviderTransactionAsync(transaction.Provider, transaction.ProviderTransactionId, cancellationToken);

        var orderBefore = await services.GetRequiredService<IOrderRepository>()
            .GetByIdAsync(transaction.OrderId, cancellationToken);

        var result = await services.GetRequiredService<ProcessPaymentCommandHandler>().Handle(
            new ProcessPaymentCommand(
                transaction.OrderId,
                transaction.Amount,
                transaction.Provider,
                transaction.ProviderTransactionId,
                PaymentAttemptStatus.Succeeded),
            cancellationToken);

        return (alreadyRecorded is null, result.OrderStatus != orderBefore?.Status);
    }

    private static string ReasonFor(Exception ex) => ex switch
    {
        InvalidOrderStateException or ConflictException or NotFoundException => ex.Message,

        // Not a business decision (lost a rowversion race, database hiccup): safe to run again.
        _ => $"Could not be processed ({ex.GetType().Name}). Run reconciliation again.",
    };
}
