using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Payments.ProcessPayment;

public sealed record ProcessPaymentCommand(
    int OrderId,
    decimal Amount,
    string Provider,
    string? ProviderTransactionId,
    PaymentAttemptStatus Status)
    : ICommand<ProcessPaymentResult>;