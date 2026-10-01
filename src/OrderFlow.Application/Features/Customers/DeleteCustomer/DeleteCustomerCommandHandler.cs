using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.DeleteCustomer;

/// <summary>
/// Hard delete. Blocked (409) if the customer has ANY order, regardless of status —
/// matches the DB's own FK: <c>orders.customer_id -&gt; customers.id</c> is
/// <c>ON DELETE RESTRICT</c>, so even a Cancelled order would make the DB reject the
/// delete anyway. This check exists to turn that into a clean 409 instead of a raw
/// SQL FK-violation 500.
/// </summary>
public sealed class DeleteCustomerCommandHandler : ICommandHandler<DeleteCustomerCommand, DeleteCustomerResult>
{
    private readonly ICustomerRepository _customers;
    private readonly IOrderReportRepository _orderReports;

    public DeleteCustomerCommandHandler(ICustomerRepository customers, IOrderReportRepository orderReports)
    {
        _customers = customers;
        _orderReports = orderReports;
    }

    public async Task<DeleteCustomerResult> Handle(
        DeleteCustomerCommand command,
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

        var totals = await _orderReports.GetCustomerTotalsAsync(customer.Id, cancellationToken);

        if (totals.OrderCount > 0)
        {
            throw new ConflictException(
                $"Customer '{customer.Id}' cannot be deleted: {totals.OrderCount} order(s) reference it.");
        }

        await _customers.DeleteAsync(customer, cancellationToken);

        return new DeleteCustomerResult(customer.Id);
    }
}
