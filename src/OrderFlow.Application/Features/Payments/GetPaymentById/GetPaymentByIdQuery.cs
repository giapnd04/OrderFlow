using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Payments.GetPaymentById;

public sealed record GetPaymentByIdQuery(int PaymentAttemptId) : IQuery<GetPaymentByIdResult>;
