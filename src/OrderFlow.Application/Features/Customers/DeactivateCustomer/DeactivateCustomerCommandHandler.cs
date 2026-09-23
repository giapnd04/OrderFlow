using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.DeactivateCustomer;

public sealed class DeactivateCustomerCommandHandler
    : ICommandHandler<DeactivateCustomerCommand, DeactivateCustomerResult>
{
    private readonly ICustomerRepository _customers;

    public DeactivateCustomerCommandHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<DeactivateCustomerResult> Handle(
        DeactivateCustomerCommand command,
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

        customer.Deactivate();

        await _customers.UpdateAsync(customer, cancellationToken);

        return new DeactivateCustomerResult(customer.Id, customer.Status.ToString());
    }
}
