using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.GetCustomerById;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Customers;

public sealed class GetCustomerByIdQueryHandlerTests
{
    [Fact]
    public async Task Handle_ExistingCustomer_ReturnsCustomer()
    {
        var customer = Customer.Create("Nguyen Van A", "a@example.com", "0900000000");
        customer.Id = 1;
        var handler = new GetCustomerByIdQueryHandler(new FakeCustomerRepository(customer));

        var result = await handler.Handle(new GetCustomerByIdQuery(customer.Id));

        Assert.Equal(customer.Id, result.CustomerId);
        Assert.Equal("Nguyen Van A", result.Name);
        Assert.Equal("a@example.com", result.Email);
        Assert.Equal("0900000000", result.Phone);
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = new GetCustomerByIdQueryHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new GetCustomerByIdQuery(999)));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Customer? _customer;

        public FakeCustomerRepository(Customer? customer) => _customer = customer;

        public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomerById tests.");

        public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
            => Task.FromResult(_customer is not null && _customer.Id == customerId ? _customer : null);

        public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomerById tests.");

        public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
            string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomerById tests.");

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomerById tests.");

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomerById tests.");

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomerById tests.");
    }
}
