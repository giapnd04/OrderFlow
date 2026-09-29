namespace OrderFlow.Application.Features.Payments.GetPaymentById;

public sealed record GetPaymentByIdResult(
    int PaymentAttemptId,
    int OrderId,
    decimal Amount,
    string Status,
    string Provider,
    string? ProviderTransactionId);
