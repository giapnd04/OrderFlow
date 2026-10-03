using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Orders;

internal sealed class FakeOrderReportRepository : IOrderReportRepository
{
    private readonly IReadOnlyList<OrderStatusTotals> _statusTotals;
    private readonly CustomerOrderTotals _customerTotals;

    public FakeOrderReportRepository(
        IReadOnlyList<OrderStatusTotals>? statusTotals = null,
        CustomerOrderTotals? customerTotals = null)
    {
        _statusTotals = statusTotals ?? Array.Empty<OrderStatusTotals>();
        _customerTotals = customerTotals ?? new CustomerOrderTotals(0, 0m, null);
    }

    public Task<IReadOnlyList<OrderStatusTotals>> GetStatusTotalsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(_statusTotals);

    public Task<CustomerOrderTotals> GetCustomerTotalsAsync(int customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_customerTotals);
}

internal sealed class ExistsOnlyFakeCustomerRepository : ICustomerRepository
{
        public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for this test.");

        public Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for this test.");

    private readonly HashSet<int> _ids;

    public ExistsOnlyFakeCustomerRepository(params int[] ids) => _ids = new HashSet<int>(ids);

    public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(_ids.Contains(customerId));

    public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
        string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
