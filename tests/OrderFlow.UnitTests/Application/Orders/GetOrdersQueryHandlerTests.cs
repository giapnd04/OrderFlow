using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.GetOrders;
using OrderFlow.Application.Features.Orders.GetOrdersAfter;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Orders;

public class GetOrdersQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingOrders_ReturnsPagedResult()
    {
        var orders = CreateOrders();

        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(orders));

        var result = await handler.Handle(
            new GetOrdersQuery(
                PageNumber: 1,
                PageSize: 2));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.PageNumber);
        Assert.Equal(2, result.PageSize);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
    }

    [Fact]
    public async Task Handle_FilterByStatus_ReturnsOnlyMatchingOrders()
    {
        var orders = CreateOrders();

        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(orders));

        var result = await handler.Handle(
            new GetOrdersQuery(
                PageNumber: 1,
                PageSize: 20,
                Status: OrderStatus.PendingPayment));

        Assert.Equal(2, result.Items.Count);
        Assert.All(
            result.Items,
            item => Assert.Equal(
                nameof(OrderStatus.PendingPayment),
                item.Status));
    }

    [Fact]
    public async Task Handle_FilterByCustomerId_ReturnsOnlyMatchingOrders()
    {
        var orders = CreateOrders();

        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(orders));

        var result = await handler.Handle(
            new GetOrdersQuery(
                PageNumber: 1,
                PageSize: 20,
                CustomerId: 1));

        Assert.Equal(3, result.Items.Count);
        Assert.All(
            result.Items,
            item => Assert.Equal(1, item.CustomerId));
    }

    [Fact]
    public async Task Handle_FilterByStatusAndCustomerId_ReturnsMatchingOrders()
    {
        var orders = CreateOrders();

        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(orders));

        var result = await handler.Handle(
            new GetOrdersQuery(
                PageNumber: 1,
                PageSize: 20,
                Status: OrderStatus.Paid,
                CustomerId: 1));

        Assert.Single(result.Items);
        Assert.Equal(1, result.Items.Single().CustomerId);
        Assert.Equal(
            nameof(OrderStatus.Paid),
            result.Items.Single().Status);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(-1, 20)]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 101)]
    public async Task Handle_InvalidPagination_ThrowsValidationException(
        int pageNumber,
        int pageSize)
    {
        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(CreateOrders()));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(
                new GetOrdersQuery(
                    pageNumber,
                    pageSize)));
    }

    [Fact]
    public async Task Handle_NoMatchingOrders_ReturnsEmptyResult()
    {
        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(CreateOrders()));

        var result = await handler.Handle(
            new GetOrdersQuery(
                PageNumber: 1,
                PageSize: 20,
                CustomerId: 999));

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
    }

    [Fact]
    public async Task Handle_PageBeyondData_ReturnsEmptyPageWithCorrectTotalCount()
    {
        var handler = new GetOrdersQueryHandler(
            new FakeOrderRepository(CreateOrders()));

        var result = await handler.Handle(
            new GetOrdersQuery(
                PageNumber: 5,
                PageSize: 2));

        Assert.Empty(result.Items);
        Assert.Equal(5, result.TotalCount);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(5, result.PageNumber);
        Assert.Equal(2, result.PageSize);
    }

    private static List<Order> CreateOrders()
    {
        // customer 1 -> 3 orders (Pending, Paid, Cancelled); customer 2 -> 1 (Pending);
        // customer 3 -> 1 (Confirmed). Exactly 2 PendingPayment orders overall.
        var pending1 = Order.Create(1, new[] { OrderItem.Create(1, 1, 100m) });

        var paid1 = Order.Create(1, new[] { OrderItem.Create(2, 2, 100m) });
        paid1.MarkAsPaid();

        var cancelled1 = Order.Create(1, new[] { OrderItem.Create(3, 1, 200m) });
        cancelled1.Cancel();

        var pending2 = Order.Create(2, new[] { OrderItem.Create(4, 1, 300m) });

        var confirmed3 = Order.Create(3, new[] { OrderItem.Create(5, 1, 400m) });
        confirmed3.MarkAsPaid();
        confirmed3.Confirm();

        var orders = new List<Order> { pending1, paid1, cancelled1, pending2, confirmed3 };

        for (var i = 0; i < orders.Count; i++)
        {
            orders[i].Id = i + 1;
        }

        return orders;
    }

    [Fact]
    public async Task HandleAfter_WalksEveryOrderOnceInIdOrder()
    {
        var handler = new GetOrdersAfterQueryHandler(new FakeOrderRepository(CreateOrders()));

        var first = await handler.Handle(new GetOrdersAfterQuery(AfterId: null, PageSize: 2));
        var second = await handler.Handle(new GetOrdersAfterQuery(first.NextAfterId, PageSize: 2));
        var last = await handler.Handle(new GetOrdersAfterQuery(second.NextAfterId, PageSize: 2));

        Assert.Equal([1, 2], first.Items.Select(i => i.OrderId));
        Assert.Equal([3, 4], second.Items.Select(i => i.OrderId));
        Assert.Equal([5], last.Items.Select(i => i.OrderId));
        Assert.Equal(2, first.NextAfterId);
        Assert.Null(last.NextAfterId);
    }

    [Fact]
    public async Task HandleAfter_ExactlyOneFullPageLeft_HasNoNextCursor()
    {
        var handler = new GetOrdersAfterQueryHandler(new FakeOrderRepository(CreateOrders()));

        var result = await handler.Handle(new GetOrdersAfterQuery(AfterId: 3, PageSize: 2));

        Assert.Equal([4, 5], result.Items.Select(i => i.OrderId));
        Assert.Null(result.NextAfterId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task HandleAfter_InvalidPageSize_ThrowsValidationException(int pageSize)
    {
        var handler = new GetOrdersAfterQueryHandler(new FakeOrderRepository(CreateOrders()));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new GetOrdersAfterQuery(null, pageSize)));
    }

    private sealed class FakeOrderRepository : IOrderQueries
    {
        private readonly List<Order> _orders;

        public FakeOrderRepository(IEnumerable<Order> orders)
        {
            _orders = orders.ToList();
        }

        public Task<(IReadOnlyList<OrderListItem> Items, int TotalCount)> GetPageAsync(
            OrderStatus? status,
            int? customerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var filtered = Filtered(status, customerId).ToList();

            var page = filtered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<(IReadOnlyList<OrderListItem> Items, int TotalCount)>((page, filtered.Count));
        }

        public Task<IReadOnlyList<OrderListItem>> GetPageAfterAsync(
            OrderStatus? status,
            int? customerId,
            int? afterId,
            int take,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<OrderListItem>>(Filtered(status, customerId)
                .Where(order => afterId is null || order.OrderId > afterId)
                .Take(take)
                .ToList());

        private IEnumerable<OrderListItem> Filtered(OrderStatus? status, int? customerId)
            => _orders
                .Where(order => status is null || order.Status == status)
                .Where(order => customerId is null || order.CustomerId == customerId)
                .OrderBy(order => order.Id)
                .Select(order => new OrderListItem(order.Id, order.CustomerId, order.Status, order.TotalAmount));
    }
}
