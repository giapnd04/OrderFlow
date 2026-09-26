namespace OrderFlow.Application.Features.Payments.GetOrderPaymentSummary;

/// <summary>
/// PaidAmount is derived from succeeded payment attempts, not from Order.Status —
/// payment_attempt is the source of truth (schema v1 review), so if the two ever
/// disagree this summary shows the real paid amount.
/// </summary>
public sealed record GetOrderPaymentSummaryResult(
    int OrderId,
    string OrderStatus,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal RemainingAmount,
    bool IsFullyPaid);
