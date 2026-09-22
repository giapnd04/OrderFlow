using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.UpdateCustomer;

public sealed class UpdateCustomerCommandHandler : ICommandHandler<UpdateCustomerCommand, UpdateCustomerResult>
{
    private readonly ICustomerRepository _customers;

    public UpdateCustomerCommandHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<UpdateCustomerResult> Handle(
        UpdateCustomerCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.CustomerId <= 0)
        {
            throw new ValidationException("CustomerId must be greater than zero.");
        }

        var customer = await _customers.GetByIdForUpdateAsync(command.CustomerId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", command.CustomerId);
        }

        customer.UpdateProfile(command.Name, command.Phone);

        await _customers.UpdateAsync(customer, cancellationToken);

        return new UpdateCustomerResult(customer.Id, customer.Name, customer.Email, customer.Phone);
    }
}
