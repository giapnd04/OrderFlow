using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Payments.GetOrderPayments;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Payments;

public sealed class GetOrderPaymentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_OrderWithAttempts_ReturnsOnlyThatOrdersAttemptsInOrder()
    {
        var order = PaymentTestData.OrderWithTotal(1, 100m);
        var payments = new FakePaymentRepository(
            PaymentTestData.Attempt(2, 1, 40m, PaymentAttemptStatus.Succeeded),
            PaymentTestData.Attempt(1, 1, 100m, PaymentAttemptStatus.Failed),
            PaymentTestData.Attempt(3, 99, 5m, PaymentAttemptStatus.Succeeded));
        var handler = new GetOrderPaymentsQueryHandler(new ReadOnlyFakeOrderRepository(order), payments);

        var result = await handler.Handle(new GetOrderPaymentsQuery(1));

        Assert.Equal(1, result.OrderId);
        Assert.Collection(
            result.Payments,
            p => { Assert.Equal(1, p.PaymentAttemptId); Assert.Equal(nameof(PaymentAttemptStatus.Failed), p.Status); },
            p => { Assert.Equal(2, p.PaymentAttemptId); Assert.Equal(40m, p.Amount); Assert.Equal("Stripe", p.Provider); });
    }

    [Fact]
    public async Task Handle_OrderWithoutAttempts_ReturnsEmptyList()
    {
        var handler = new GetOrderPaymentsQueryHandler(
            new ReadOnlyFakeOrderRepository(PaymentTestData.OrderWithTotal(1, 100m)),
            new FakePaymentRepository());

        var result = await handler.Handle(new GetOrderPaymentsQuery(1));

        Assert.Empty(result.Payments);
    }

    [Fact]
    public async Task Handle_UnknownOrder_ThrowsNotFound()
    {
        var handler = new GetOrderPaymentsQueryHandler(new ReadOnlyFakeOrderRepository(null), new FakePaymentRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetOrderPaymentsQuery(999)));
    }

    [Fact]
    public async Task Handle_InvalidOrderId_ThrowsValidation()
    {
        var handler = new GetOrderPaymentsQueryHandler(new ReadOnlyFakeOrderRepository(null), new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetOrderPaymentsQuery(0)));
    }
}
