using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Payments.GetPayments;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Payments;

public sealed class GetPaymentsQueryHandlerTests
{
    [Fact]
    public async Task Handle_FilterByStatus_ReturnsOnlyMatching()
    {
        var handler = new GetPaymentsQueryHandler(new FakePaymentRepository(
            PaymentTestData.Attempt(1, 1, 100m, PaymentAttemptStatus.Succeeded),
            PaymentTestData.Attempt(2, 1, 50m, PaymentAttemptStatus.Failed)));

        var result = await handler.Handle(new GetPaymentsQuery(Status: PaymentAttemptStatus.Succeeded));

        var item = Assert.Single(result.Items);
        Assert.Equal(1, item.PaymentAttemptId);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Handle_ValidRequest_ComputesTotalPages()
    {
        var handler = new GetPaymentsQueryHandler(new FakePaymentRepository(
            PaymentTestData.Attempt(1, 1, 10m, PaymentAttemptStatus.Succeeded),
            PaymentTestData.Attempt(2, 2, 10m, PaymentAttemptStatus.Succeeded),
            PaymentTestData.Attempt(3, 3, 10m, PaymentAttemptStatus.Succeeded)));

        var result = await handler.Handle(new GetPaymentsQuery(PageNumber: 1, PageSize: 2));

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task Handle_ZeroPageSize_ThrowsValidation()
    {
        var handler = new GetPaymentsQueryHandler(new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetPaymentsQuery(PageSize: 0)));
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_ThrowsValidation()
    {
        var handler = new GetPaymentsQueryHandler(new FakePaymentRepository());

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetPaymentsQuery(PageSize: 101)));
    }
}
