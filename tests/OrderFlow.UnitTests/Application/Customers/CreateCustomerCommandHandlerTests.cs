using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Application.Features.Customers.CreateCustomer;
using OrderFlow.Domain.Entities;
using OrderFlow.Domain.Exceptions;

namespace OrderFlow.UnitTests.Application.Customers;

public class CreateCustomerCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_CreatesCustomer()
    {
        var customers = new FakeCustomerRepository();
        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "John Doe",
            "john@example.com",
            "0123456789");

        var result = await handler.Handle(command);

        Assert.True(result.CustomerId > 0);
        Assert.Equal("John Doe", result.Name);
        Assert.Equal("john@example.com", result.Email);
        Assert.Equal("0123456789", result.Phone);

        Assert.NotNull(customers.Saved);
        Assert.Equal("John Doe", customers.Saved!.Name);
        Assert.Equal("john@example.com", customers.Saved.Email);
        Assert.Equal("0123456789", customers.Saved.Phone);
    }

    [Fact]
    public async Task Handle_NameIsEmpty_ThrowsValidation()
    {
        var customers = new FakeCustomerRepository();
        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "",
            "john@example.com",
            null);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_EmailIsEmpty_ThrowsValidation()
    {
        var customers = new FakeCustomerRepository();
        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "John Doe",
            "",
            null);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_InvalidEmail_ThrowsValidation()
    {
        var customers = new FakeCustomerRepository();
        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "John Doe",
            "not-an-email",
            null);

        await Assert.ThrowsAsync<DomainException>(
            () => handler.Handle(command));
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ThrowsValidation()
    {
        var customers = new FakeCustomerRepository(
            Customer.Create(
                "Existing Customer",
                "john@example.com"));

        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "Another Customer",
            "john@example.com",
            null);

        await Assert.ThrowsAsync<ValidationException>(
            () => handler.Handle(command));

        Assert.Null(customers.Saved);
    }

    [Fact]
    public async Task Handle_EmailIsTrimmed_CreatesCustomerWithTrimmedEmail()
    {
        var customers = new FakeCustomerRepository();
        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "John Doe",
            "  john@example.com  ",
            null);

        var result = await handler.Handle(command);

        Assert.Equal("john@example.com", result.Email);
        Assert.Equal("john@example.com", customers.Saved!.Email);
    }

    [Fact]
    public async Task Handle_OptionalPhone_CreatesCustomerWithoutPhone()
    {
        var customers = new FakeCustomerRepository();
        var handler = new CreateCustomerCommandHandler(customers);

        var command = new CreateCustomerCommand(
            "John Doe",
            "john@example.com",
            null);

        var result = await handler.Handle(command);

        Assert.Null(result.Phone);
        Assert.Null(customers.Saved!.Phone);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        private readonly List<Customer> _customers;

        public Customer? Saved { get; private set; }

        public FakeCustomerRepository(params Customer[] customers)
        {
            _customers = customers.ToList();
        }

        public Task<bool> ExistsAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _customers.Any(c => c.Id == customerId));
        }

        public Task<Customer?> GetByIdAsync(
            int customerId,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExistsByEmailAsync(
            string email,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _customers.Any(c =>
                    string.Equals(
                        c.Email,
                        email,
                        StringComparison.OrdinalIgnoreCase)));
        }

        public Task<bool> AddAsync(Customer customer, CancellationToken cancellationToken = default)
        {
            customer.Id = _customers.Count + 1;

            _customers.Add(customer);
            Saved = customer;

            return Task.FromResult(true);
        }
    }
}