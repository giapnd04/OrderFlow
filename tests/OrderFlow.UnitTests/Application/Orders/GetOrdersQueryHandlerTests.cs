using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Orders.GetOrders;
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
        var orders = new List<Order>();

        orders.Add(Order.Create(
            1,
            new[]
            {
                OrderItem.Create(1, 1, 100m)
            }));

        orders.Add(Order.Create(
            1,
            new[]
            {
                OrderItem.Create(2, 2, 100m)
            }));

        orders.Add(Order.Create(
            2,
            new[]
            {
                OrderItem.Create(3, 1, 200m)
            }));

        orders.Add(Order.Create(
            2,
            new[]
            {
                OrderItem.Create(4, 1, 300m)
            }));

        orders.Add(Order.Create(
            3,
            new[]
            {
                OrderItem.Create(5, 1, 400m)
            }));

        return orders;
    }

    private sealed class FakeOrderRepository : IOrderRepository
    {
        private readonly List<Order> _orders;

        public FakeOrderRepository(IEnumerable<Order> orders)
        {
            _orders = orders.ToList();
        }

        public Task AddAsync(
            Order order,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Not needed for GetOrders tests.");

        public Task<Order?> GetByIdAsync(
            int orderId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Not needed for GetOrders tests.");

        public Task<Order?> GetByIdForUpdateAsync(
            int orderId,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Not needed for GetOrders tests.");

        public Task UpdateAsync(
            Order order,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException(
                "Not needed for GetOrders tests.");

        public Task<(
            IReadOnlyCollection<Order> Orders,
            int TotalCount)> GetPagedAsync(
            OrderStatus? status,
            int? customerId,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<Order> query = _orders;

            if (status.HasValue)
            {
                query = query.Where(
                    order => order.Status == status.Value);
            }

            if (customerId.HasValue)
            {
                query = query.Where(
                    order => order.CustomerId == customerId.Value);
            }

            var filteredOrders = query
                .OrderBy(order => order.Id)
                .ToList();

            var totalCount = filteredOrders.Count;

            var page = filteredOrders
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<(
                IReadOnlyCollection<Order> Orders,
                int TotalCount)>((page, totalCount));
        }
    }
}