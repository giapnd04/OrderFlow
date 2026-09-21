using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.GetCustomerById;

public sealed class GetCustomerByIdQueryHandler
    : IQueryHandler<GetCustomerByIdQuery, GetCustomerByIdResult>
{
    private readonly ICustomerRepository _customers;

    public GetCustomerByIdQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<GetCustomerByIdResult> Handle(
        GetCustomerByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var customer = await _customers.GetByIdAsync(query.CustomerId, cancellationToken);

        if (customer is null)
        {
            throw new NotFoundException("Customer", query.CustomerId);
        }

        return new GetCustomerByIdResult(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone);
    }
}
