using Microsoft.Extensions.Logging;

namespace OrderFlow.Application.Logging;

/// <summary>
/// Business events written to the log (OF-10). Each has a fixed EventId and named fields, so
/// logs can be filtered by event ("all OrderPaid today") as well as by correlation id. Only ids,
/// amounts and statuses are logged: never names, emails, passwords, OTPs or tokens.
/// Handlers log after their transaction commits, so a rolled-back change is never reported.
/// </summary>
public static partial class BusinessEvents
{
    [LoggerMessage(EventId = 1001, EventName = "OrderCreated", Level = LogLevel.Information,
        Message = "Order {OrderId} created for customer {CustomerId}: {LineCount} line(s), total {TotalAmount}")]
    public static partial void OrderCreated(this ILogger logger, int orderId, int customerId, int lineCount, decimal totalAmount);

    [LoggerMessage(EventId = 1002, EventName = "PaymentRecorded", Level = LogLevel.Information,
        Message = "Payment {PaymentAttemptId} of {Amount} recorded for order {OrderId} via {Provider} with status {PaymentStatus}")]
    public static partial void PaymentRecorded(
        this ILogger logger, int paymentAttemptId, int orderId, decimal amount, string provider, string paymentStatus);

    [LoggerMessage(EventId = 1003, EventName = "OrderPaid", Level = LogLevel.Information,
        Message = "Order {OrderId} fully paid")]
    public static partial void OrderPaid(this ILogger logger, int orderId);

    [LoggerMessage(EventId = 1004, EventName = "OrderCancelled", Level = LogLevel.Information,
        Message = "Order {OrderId} cancelled from {PreviousStatus}")]
    public static partial void OrderCancelled(this ILogger logger, int orderId, string previousStatus);

    [LoggerMessage(EventId = 1005, EventName = "ReconciliationApplied", Level = LogLevel.Information,
        Message = "Reconciliation checked {Checked} charge(s): {PaymentsRecorded} payment(s) recorded, {OrdersCompleted} order(s) completed, {NeedsReview} need review")]
    public static partial void ReconciliationApplied(
        this ILogger logger, int @checked, int paymentsRecorded, int ordersCompleted, int needsReview);

    [LoggerMessage(EventId = 1006, EventName = "ConcurrencyConflict", Level = LogLevel.Warning,
        Message = "Concurrency conflict on {Method} {Path}: the row changed after it was read")]
    public static partial void ConcurrencyConflict(this ILogger logger, string method, string path);
}
