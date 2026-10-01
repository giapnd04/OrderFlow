using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.DeleteCustomer;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Customers;

public sealed class DeleteCustomerCommandHandlerTests
{
    private static Customer ExistingCustomer()
    {
        var customer = Customer.Create("Nguyen Van A", "a@example.com");
        customer.Id = 1;
        return customer;
    }

    [Fact]
    public async Task Handle_CustomerWithNoOrders_DeletesIt()
    {
        var customer = ExistingCustomer();
        var customers = new FakeCustomerRepository(customer);
        var handler = new DeleteCustomerCommandHandler(customers, new FakeOrderReportRepository(0));

        var result = await handler.Handle(new DeleteCustomerCommand(customer.Id));

        Assert.Equal(customer.Id, result.CustomerId);
        Assert.True(customers.DeleteCalled);
    }

    [Fact]
    public async Task Handle_CustomerWithOrders_ThrowsConflictAndDoesNotDelete()
    {
        var customer = ExistingCustomer();
        var customers = new FakeCustomerRepository(customer);
        var handler = new DeleteCustomerCommandHandler(customers, new FakeOrderReportRepository(3));

        await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new DeleteCustomerCommand(customer.Id)));

        Assert.False(customers.DeleteCalled);
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = new DeleteCustomerCommandHandler(new FakeCustomerRepository(customer: null), new FakeOrderReportRepository(0));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new DeleteCustomerCommand(999)));
    }

    [Fact]
    public async Task Handle_InvalidCustomerId_ThrowsValidation()
    {
        var handler = new DeleteCustomerCommandHandler(new FakeCustomerRepository(customer: null), new FakeOrderReportRepository(0));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new DeleteCustomerCommand(0)));
    }

    private sealed class FakeOrderReportRepository : IOrderReportRepository
    {
        private readonly int _orderCount;

        public FakeOrderReportRepository(int orderCount) => _orderCount = orderCount;

        public Task<IReadOnlyList<OrderStatusTotals>> GetStatusTotalsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<CustomerOrderTotals> GetCustomerTotalsAsync(int customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(new CustomerOrderTotals(_orderCount, 0m, null));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Customer? _customer;

        public bool DeleteCalled { get; private set; }

        public FakeCustomerRepository(Customer? customer) => _customer = customer;

        public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customer is not null && _customer.Id == customerId ? _customer : null);

        public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
            string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            DeleteCalled = true;
            return Task.CompletedTask;
        }
    }
}
