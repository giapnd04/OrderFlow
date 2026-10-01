using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.ChangeCustomerEmail;

/// <summary>
/// Dedicated use case for the one field <see cref="UpdateCustomer.UpdateCustomerCommandHandler"/>
/// deliberately excludes: email is the unique login/lookup identity, so changing it gets its
/// own uniqueness check rather than being folded into the general profile update.
/// </summary>
public sealed class ChangeCustomerEmailCommandHandler
    : ICommandHandler<ChangeCustomerEmailCommand, ChangeCustomerEmailResult>
{
    private readonly ICustomerRepository _customers;

    public ChangeCustomerEmailCommandHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<ChangeCustomerEmailResult> Handle(
        ChangeCustomerEmailCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId <= 0)
        {
            throw new ValidationException("CustomerId must be greater than zero.");
        }

        var email = command.Email?.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ValidationException("Customer email is required.");
        }

        var customer = await _customers.GetByIdForUpdateAsync(command.CustomerId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", command.CustomerId);
        }

        if (!string.Equals(customer.Email, email, StringComparison.OrdinalIgnoreCase))
        {
            if (await _customers.ExistsByEmailAsync(email, cancellationToken))
            {
                throw new ValidationException($"Customer with email '{email}' already exists.");
            }

            customer.ChangeEmail(email);

            await _customers.UpdateAsync(customer, cancellationToken);
        }

        return new ChangeCustomerEmailResult(customer.Id, customer.Email);
    }
}
