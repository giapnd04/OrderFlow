using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Payments.GetOrderPaymentSummary;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Payments;

public sealed class GetOrderPaymentSummaryQueryHandlerTests
{
    private static GetOrderPaymentSummaryQueryHandler HandlerFor(decimal total, params OrderFlow.Domain.Entities.PaymentAttempt[] attempts)
        => new(new ReadOnlyFakeOrderRepository(PaymentTestData.OrderWithTotal(1, total)), new FakePaymentRepository(attempts));

    [Fact]
    public async Task Handle_PartiallyPaid_CountsOnlySucceededAttempts()
    {
        var handler = HandlerFor(
            100m,
            PaymentTestData.Attempt(1, 1, 30m, PaymentAttemptStatus.Succeeded),
            PaymentTestData.Attempt(2, 1, 50m, PaymentAttemptStatus.Failed));

        var result = await handler.Handle(new GetOrderPaymentSummaryQuery(1));

        Assert.Equal(30m, result.PaidAmount);
        Assert.Equal(70m, result.RemainingAmount);
        Assert.False(result.IsFullyPaid);
        Assert.Equal(nameof(OrderStatus.PendingPayment), result.OrderStatus);
    }

    [Fact]
    public async Task Handle_FullyPaid_ReportsNothingRemaining()
    {
        var handler = HandlerFor(100m, PaymentTestData.Attempt(1, 1, 100m, PaymentAttemptStatus.Succeeded));

        var result = await handler.Handle(new GetOrderPaymentSummaryQuery(1));

        Assert.True(result.IsFullyPaid);
        Assert.Equal(0m, result.RemainingAmount);
    }

    [Fact]
    public async Task Handle_Overpaid_RemainingIsClampedToZero()
    {
        var handler = HandlerFor(100m, PaymentTestData.Attempt(1, 1, 120m, PaymentAttemptStatus.Succeeded));

        var result = await handler.Handle(new GetOrderPaymentSummaryQuery(1));

        Assert.True(result.IsFullyPaid);
        Assert.Equal(120m, result.PaidAmount);
        Assert.Equal(0m, result.RemainingAmount);
    }

    [Fact]
    public async Task Handle_NoAttempts_PaidIsZero()
    {
        var result = await HandlerFor(100m).Handle(new GetOrderPaymentSummaryQuery(1));

        Assert.Equal(0m, result.PaidAmount);
        Assert.Equal(100m, result.RemainingAmount);
        Assert.False(result.IsFullyPaid);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new GetOrderPaymentSummaryQueryHandler(new ReadOnlyFakeOrderRepository(null), new FakePaymentRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetOrderPaymentSummaryQuery(999)));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = new GetOrderPaymentSummaryQueryHandler(new ReadOnlyFakeOrderRepository(null), new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetOrderPaymentSummaryQuery(0)));
    }
}
