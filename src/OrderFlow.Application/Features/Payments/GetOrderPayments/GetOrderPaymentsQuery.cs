using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Payments.GetOrderPayments;

public sealed record GetOrderPaymentsQuery(int OrderId) : IQuery<GetOrderPaymentsResult>;
