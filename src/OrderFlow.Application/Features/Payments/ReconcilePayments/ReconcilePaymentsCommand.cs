using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Payments.ReconcilePayments;

public sealed record ReconcilePaymentsCommand : ICommand<ReconcilePaymentsResult>;

public sealed record ReconcilePaymentsResult(
    int Checked,
    int PaymentsRecorded,
    int OrdersCompleted,
    int Unchanged,
    IReadOnlyList<ReconciliationIssue> NeedsReview);

public sealed record ReconciliationIssue(
    string Provider,
    string ProviderTransactionId,
    int OrderId,
    decimal Amount,
    string Reason);
