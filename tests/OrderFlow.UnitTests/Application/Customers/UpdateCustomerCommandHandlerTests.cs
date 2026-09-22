using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.UpdateCustomer;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Customers;

public sealed class UpdateCustomerCommandHandlerTests
{
    private static Customer ExistingCustomer()
    {
        var customer = Customer.Create("Nguyen Van A", "a@example.com", "0900000000");
        customer.Id = 1;
        return customer;
    }

    [Fact]
    public async Task Handle_ValidRequest_UpdatesNameAndPhone()
    {
        var customer = ExistingCustomer();
        var handler = new UpdateCustomerCommandHandler(new FakeCustomerRepository(customer));

        var result = await handler.Handle(new UpdateCustomerCommand(customer.Id, "Nguyen Van B", "0911111111"));

        Assert.Equal("Nguyen Van B", result.Name);
        Assert.Equal("0911111111", result.Phone);
        Assert.Equal("a@example.com", result.Email);
        Assert.Equal("Nguyen Van B", customer.Name);
        Assert.Equal("0911111111", customer.Phone);
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = new UpdateCustomerCommandHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new UpdateCustomerCommand(999, "Name", null)));
    }

    [Fact]
    public async Task Handle_EmptyName_ThrowsDomainException()
    {
        var customer = ExistingCustomer();
        var handler = new UpdateCustomerCommandHandler(new FakeCustomerRepository(customer));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new UpdateCustomerCommand(customer.Id, "", null)));
    }

    [Fact]
    public async Task Handle_InvalidCustomerId_ThrowsValidation()
    {
        var handler = new UpdateCustomerCommandHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new UpdateCustomerCommand(0, "Name", null)));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Customer? _customer;

        public FakeCustomerRepository(Customer? customer) => _customer = customer;

        public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateCustomer tests.");

        public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateCustomer tests.");

        public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customer is not null && _customer.Id == customerId ? _customer : null);

        public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
            string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateCustomer tests.");

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateCustomer tests.");

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for UpdateCustomer tests.");

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
