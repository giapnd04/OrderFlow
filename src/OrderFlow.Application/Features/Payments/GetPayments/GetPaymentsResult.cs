namespace OrderFlow.Application.Features.Payments.GetPayments;

public sealed record GetPaymentsResult(
    IReadOnlyCollection<GetPaymentsItemResult> Items,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record GetPaymentsItemResult(
    int PaymentAttemptId,
    int OrderId,
    decimal Amount,
    string Status,
    string Provider,
    string? ProviderTransactionId);
