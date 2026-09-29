using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Domain.Enums;

namespace OrderFlow.Application.Features.Payments.GetPayments;

public sealed record GetPaymentsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    PaymentAttemptStatus? Status = null,
    string? Provider = null) : IQuery<GetPaymentsResult>;
