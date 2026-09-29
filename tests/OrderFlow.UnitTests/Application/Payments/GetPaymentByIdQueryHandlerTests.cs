using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Payments.GetPaymentById;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Payments;

public sealed class GetPaymentByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingPayment_ReturnsIt()
    {
        var handler = new GetPaymentByIdQueryHandler(
            new FakePaymentRepository(PaymentTestData.Attempt(1, 5, 100m, PaymentAttemptStatus.Succeeded)));

        var result = await handler.Handle(new GetPaymentByIdQuery(1));

        Assert.Equal(1, result.PaymentAttemptId);
        Assert.Equal(5, result.OrderId);
        Assert.Equal(100m, result.Amount);
        Assert.Equal(nameof(PaymentAttemptStatus.Succeeded), result.Status);
        Assert.Equal("Stripe", result.Provider);
        Assert.Equal("tx-1", result.ProviderTransactionId);
    }

    [Fact]
    public async Task Handle_UnknownPayment_ThrowsNotFound()
    {
        var handler = new GetPaymentByIdQueryHandler(new FakePaymentRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetPaymentByIdQuery(999)));
    }
}
