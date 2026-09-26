using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Payments.GetOrderPaymentSummary;

public sealed record GetOrderPaymentSummaryQuery(int OrderId) : IQuery<GetOrderPaymentSummaryResult>;
