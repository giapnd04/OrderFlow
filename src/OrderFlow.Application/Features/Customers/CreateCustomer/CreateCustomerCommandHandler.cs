using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;
using OrderFlow.Domain.Entities;

namespace OrderFlow.Application.Features.Customers.CreateCustomer;

public sealed class CreateCustomerCommandHandler
    : ICommandHandler<CreateCustomerCommand, CreateCustomerResult>
{
    private readonly ICustomerRepository _customers;

    public CreateCustomerCommandHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<CreateCustomerResult> Handle(
     CreateCustomerCommand command,
     CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ValidationException("Name is required.");
        }

        var email = command.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Customer email is required.");
        }

        if (await _customers.ExistsByEmailAsync(email, cancellationToken))
        {
            throw new ValidationException($"Customer with email '{email}' already exists.");
        }

        var customer = Customer.Create(
            command.Name,
            email,
            command.Phone);

        await _customers.AddAsync(customer, cancellationToken);

        return new CreateCustomerResult(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone);
    }
}