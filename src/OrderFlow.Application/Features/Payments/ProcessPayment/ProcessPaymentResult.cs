using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Payments.ProcessPayment;

public sealed record ProcessPaymentResult(
    int OrderId,
    OrderStatus OrderStatus,
    int PaymentAttemptId,
    PaymentAttemptStatus PaymentAttemptStatus);