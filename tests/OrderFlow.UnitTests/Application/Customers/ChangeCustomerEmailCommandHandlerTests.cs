using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.ChangeCustomerEmail;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Customers;

public sealed class ChangeCustomerEmailCommandHandlerTests
{
    private static Customer ExistingCustomer()
    {
        var customer = Customer.Create("Nguyen Van A", "a@example.com");
        customer.Id = 1;
        return customer;
    }

    [Fact]
    public async Task Handle_NewUniqueEmail_ChangesIt()
    {
        var customer = ExistingCustomer();
        var handler = new ChangeCustomerEmailCommandHandler(new FakeCustomerRepository(customer));

        var result = await handler.Handle(new ChangeCustomerEmailCommand(customer.Id, "b@example.com"));

        Assert.Equal("b@example.com", result.Email);
        Assert.Equal("b@example.com", customer.Email);
    }

    [Fact]
    public async Task Handle_SameEmailDifferentCasing_IsNoOpAndSkipsUniquenessCheck()
    {
        var customer = ExistingCustomer();
        var repo = new FakeCustomerRepository(customer);
        var handler = new ChangeCustomerEmailCommandHandler(repo);

        var result = await handler.Handle(new ChangeCustomerEmailCommand(customer.Id, "A@EXAMPLE.COM"));

        Assert.Equal("a@example.com", result.Email);
        Assert.False(repo.ExistsByEmailAsyncCalled);
    }

    [Fact]
    public async Task Handle_EmailAlreadyUsedByAnotherCustomer_ThrowsValidation()
    {
        var customer = ExistingCustomer();
        var repo = new FakeCustomerRepository(customer) { EmailExists = true };
        var handler = new ChangeCustomerEmailCommandHandler(repo);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new ChangeCustomerEmailCommand(customer.Id, "taken@example.com")));

        Assert.Equal("a@example.com", customer.Email);
    }

    [Fact]
    public async Task Handle_InvalidEmailFormat_ThrowsDomainException()
    {
        var customer = ExistingCustomer();
        var handler = new ChangeCustomerEmailCommandHandler(new FakeCustomerRepository(customer));

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(new ChangeCustomerEmailCommand(customer.Id, "not-an-email")));
    }

    [Fact]
    public async Task Handle_EmptyEmail_ThrowsValidation()
    {
        var customer = ExistingCustomer();
        var handler = new ChangeCustomerEmailCommandHandler(new FakeCustomerRepository(customer));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new ChangeCustomerEmailCommand(customer.Id, " ")));
    }

    [Fact]
    public async Task Handle_UnknownCustomer_ThrowsNotFound()
    {
        var handler = new ChangeCustomerEmailCommandHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<NotFoundException>(
            () => handler.Handle(new ChangeCustomerEmailCommand(999, "b@example.com")));
    }

    [Fact]
    public async Task Handle_InvalidCustomerId_ThrowsValidation()
    {
        var handler = new ChangeCustomerEmailCommandHandler(new FakeCustomerRepository(customer: null));

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(new ChangeCustomerEmailCommand(0, "b@example.com")));
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly Customer? _customer;

        public bool EmailExists { get; set; }

        public bool ExistsByEmailAsyncCalled { get; private set; }

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
        {
            ExistsByEmailAsyncCalled = true;
            return Task.FromResult(EmailExists);
        }

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(Customer customer, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
