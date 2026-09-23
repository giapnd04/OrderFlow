using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.ReactivateCustomer;

public sealed class ReactivateCustomerCommandHandler
    : ICommandHandler<ReactivateCustomerCommand, ReactivateCustomerResult>
{
    private readonly ICustomerRepository _customers;

    public ReactivateCustomerCommandHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<ReactivateCustomerResult> Handle(
        ReactivateCustomerCommand command,
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

        customer.Reactivate();

        await _customers.UpdateAsync(customer, cancellationToken);

        return new ReactivateCustomerResult(customer.Id, customer.Status.ToString());
    }
}
