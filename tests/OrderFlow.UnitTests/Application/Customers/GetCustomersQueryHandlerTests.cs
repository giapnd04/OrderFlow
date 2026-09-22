using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.GetCustomers;
using OrderFlow.Domain.Entities;

namespace OrderFlow.UnitTests.Application.Customers;

public sealed class GetCustomersQueryHandlerTests
{
    private static Customer NamedCustomer(int id, string name, string email)
    {
        var customer = Customer.Create(name, email);
        customer.Id = id;
        return customer;
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsPagedCustomers()
    {
        var customers = new[]
        {
            NamedCustomer(1, "Nguyen Van A", "a@example.com"),
            NamedCustomer(2, "Tran Thi B", "b@example.com"),
        };
        var handler = new GetCustomersQueryHandler(new FakeCustomerRepository(customers));

        var result = await handler.Handle(new GetCustomersQuery(PageNumber: 1, PageSize: 20));

        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.Collection(
            result.Items,
            item => Assert.Equal("Nguyen Van A", item.Name),
            item => Assert.Equal("Tran Thi B", item.Name));
    }

    [Fact]
    public async Task Handle_ZeroPageSize_ThrowsValidation()
    {
        var handler = new GetCustomersQueryHandler(new FakeCustomerRepository());

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetCustomersQuery(PageNumber: 1, PageSize: 0)));
    }

    [Fact]
    public async Task Handle_PageSizeAboveMax_ThrowsValidation()
    {
        var handler = new GetCustomersQueryHandler(new FakeCustomerRepository());

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new GetCustomersQuery(PageNumber: 1, PageSize: 101)));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers;

        public FakeCustomerRepository(params Customer[] customers) => _customers = customers.ToList();

        public Task<bool> ExistsAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomers tests.");

        public Task<Customer?> GetByIdAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomers tests.");

        public Task<Customer?> GetByIdForUpdateAsync(int customerId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomers tests.");

        public Task<(IReadOnlyCollection<Customer> Customers, int TotalCount)> GetPagedAsync(
            string? search, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
        {
            var filtered = _customers
                .OrderBy(c => c.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return Task.FromResult<(IReadOnlyCollection<Customer>, int)>((filtered, _customers.Count));
        }

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomers tests.");

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomers tests.");

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not needed for GetCustomers tests.");
    }
}
