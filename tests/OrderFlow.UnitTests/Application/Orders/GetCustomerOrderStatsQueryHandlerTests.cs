using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.GetCustomerOrderStats;

namespace OrderFlow.UnitTests.Application.Orders;

public sealed class GetCustomerOrderStatsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingCustomer_ReturnsTotals()
    {
        var lastOrderAt = new DateTime(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);
        var handler = new GetCustomerOrderStatsQueryHandler(
            new ExistsOnlyFakeCustomerRepository(1),
            new FakeOrderReportRepository(customerTotals: new CustomerOrderTotals(4, 700m, lastOrderAt)));

        var result = await handler.Handle(new GetCustomerOrderStatsQuery(1));

        Assert.Equal(1, result.CustomerId);
        Assert.Equal(4, result.OrderCount);
        Assert.Equal(700m, result.SpentAmount);
        Assert.Equal(lastOrderAt, result.LastOrderAt);
    }

    [Fact]
    public async Task Handle_CustomerWithoutOrders_ReturnsZerosAndNullDate()
    {
        var handler = new GetCustomerOrderStatsQueryHandler(
            new ExistsOnlyFakeCustomerRepository(1),
            new FakeOrderReportRepository());

        var result = await handler.Handle(new GetCustomerOrderStatsQuery(1));

        Assert.Equal(0, result.OrderCount);
        Assert.Equal(0m, result.SpentAmount);
        Assert.Null(result.LastOrderAt);
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = new GetCustomerOrderStatsQueryHandler(
            new ExistsOnlyFakeCustomerRepository(),
            new FakeOrderReportRepository());

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetCustomerOrderStatsQuery(999)));
    }

    [Fact]
    public async Task Handle_InvalidCustomerId_ThrowsValidation()
    {
        var handler = new GetCustomerOrderStatsQueryHandler(
            new ExistsOnlyFakeCustomerRepository(),
            new FakeOrderReportRepository());

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetCustomerOrderStatsQuery(0)));
    }
}
