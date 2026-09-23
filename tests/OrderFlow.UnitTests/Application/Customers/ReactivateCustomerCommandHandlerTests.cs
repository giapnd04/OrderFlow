using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.ReactivateCustomer;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Enums;

namespace OrderFlow.UnitTests.Application.Customers;

public sealed class ReactivateCustomerCommandHandlerTests
{
    private static Customer InactiveCustomer()
    {
        var customer = Customer.Create("Nguyen Van A", "a@example.com");
        customer.Id = 1;
        customer.Deactivate();
        return customer;
    }

    [Fact]
    public async Task Handle_InactiveCustomer_TransitionsToActive()
    {
        var customer = InactiveCustomer();
        var handler = new ReactivateCustomerCommandHandler(new FakeCustomerRepository(customer));

        var result = await handler.Handle(new ReactivateCustomerCommand(customer.Id));

        Assert.Equal(nameof(CustomerStatus.Active), result.Status);
        Assert.Equal(CustomerStatus.Active, customer.Status);
    }

    [Fact]
    public async Task Handle_AlreadyActiveCustomer_IsIdempotent()
    {
        var customer = Customer.Create("Nguyen Van A", "a@example.com");
        customer.Id = 1;
        var handler = new ReactivateCustomerCommandHandler(new FakeCustomerRepository(customer));

        var result = await handler.Handle(new ReactivateCustomerCommand(customer.Id));

        Assert.Equal(nameof(CustomerStatus.Active), result.Status);
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = new ReactivateCustomerCommandHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ReactivateCustomerCommand(999)));
    }

    [Fact]
    public async Task Handle_InvalidCustomerId_ThrowsValidation()
    {
        var handler = new ReactivateCustomerCommandHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(new ReactivateCustomerCommand(0)));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Customer? _customer;

        public FakeCustomerRepository(Customer? customer) => _customer = customer;

        public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ReactivateCustomer tests.");

        public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ReactivateCustomer tests.");

        public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customer is not null && _customer.Id == customerId ? _customer : null);

        public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
            string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ReactivateCustomer tests.");

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ReactivateCustomer tests.");

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for ReactivateCustomer tests.");

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
