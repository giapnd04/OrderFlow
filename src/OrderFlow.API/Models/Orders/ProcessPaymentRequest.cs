using OrderFlow.Domain.Enums;

namespace OrderFlow.API.Models.Orders;

public sealed record ProcessPaymentRequest(
    decimal Amount,
    string Provider,
    string? ProviderTransactionId,
    PaymentAttemptStatus Status);