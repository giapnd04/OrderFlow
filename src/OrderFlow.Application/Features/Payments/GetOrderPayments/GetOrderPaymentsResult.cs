namespace OrderFlow.Application.Features.Payments.GetOrderPayments;

public sealed record GetOrderPaymentsResult(
    int OrderId,
    IReadOnlyCollection<GetOrderPaymentsItemResult> Payments);

public sealed record GetOrderPaymentsItemResult(
    int PaymentAttemptId,
    decimal Amount,
    string Status,
    string Provider,
    string? ProviderTransactionId);
