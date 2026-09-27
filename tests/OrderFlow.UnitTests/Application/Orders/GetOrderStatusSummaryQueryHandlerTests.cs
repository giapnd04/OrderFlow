using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Features.Orders.GetOrderStatusSummary;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Orders;

public sealed class GetOrderStatusSummaryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEveryStatusIncludingEmptyOnes()
    {
        var handler = new GetOrderStatusSummaryQueryHandler(new FakeOrderReportRepository(
            new[]
            {
                new OrderStatusTotals(OrderStatus.PendingPayment, 2, 300m),
                new OrderStatusTotals(OrderStatus.Delivered, 1, 100m),
            }));

        var result = await handler.Handle(new GetOrderStatusSummaryQuery());

        Assert.Equal(Enum.GetValues<OrderStatus>().Length, result.Statuses.Count);

        var pending = result.Statuses.Single(s => s.Status == nameof(OrderStatus.PendingPayment));
        Assert.Equal(2, pending.OrderCount);
        Assert.Equal(300m, pending.TotalAmount);

        var cancelled = result.Statuses.Single(s => s.Status == nameof(OrderStatus.Cancelled));
        Assert.Equal(0, cancelled.OrderCount);
        Assert.Equal(0m, cancelled.TotalAmount);
    }

    [Fact]
    public async Task Handle_SumsGrandTotalsAcrossStatuses()
    {
        var handler = new GetOrderStatusSummaryQueryHandler(new FakeOrderReportRepository(
            new[]
            {
                new OrderStatusTotals(OrderStatus.Paid, 3, 450m),
                new OrderStatusTotals(OrderStatus.Shipped, 2, 50m),
            }));

        var result = await handler.Handle(new GetOrderStatusSummaryQuery());

        Assert.Equal(5, result.TotalOrderCount);
        Assert.Equal(500m, result.TotalAmount);
    }

    [Fact]
    public async Task Handle_NoOrders_ReturnsAllZeros()
    {
        var handler = new GetOrderStatusSummaryQueryHandler(new FakeOrderReportRepository());

        var result = await handler.Handle(new GetOrderStatusSummaryQuery());

        Assert.Equal(0, result.TotalOrderCount);
        Assert.Equal(0m, result.TotalAmount);
        Assert.All(result.Statuses, s => Assert.Equal(0, s.OrderCount));
    }
}
