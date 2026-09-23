using OrderFlow.Application.Abstractions.Messaging;
using OrderFlow.Application.Abstractions.Persistence;
using OrderFlow.Application.Exceptions;

namespace OrderFlow.Application.Features.Customers.GetCustomers;

public sealed class GetCustomersQueryHandler
    : IQueryHandler<GetCustomersQuery, GetCustomersResult>
{
    private const int MaxPageSize = 100;

    private readonly ICustomerRepository _customers;

    public GetCustomersQueryHandler(ICustomerRepository customers)
    {
        _customers = customers;
    }

    public async Task<GetCustomersResult> Handle(
        GetCustomersQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.PageNumber <= 0)
        {
            throw new ValidationException("PageNumber must be greater than zero.");
        }

        if (query.PageSize <= 0)
        {
            throw new ValidationException("PageSize must be greater than zero.");
        }

        if (query.PageSize > MaxPageSize)
        {
            throw new ValidationException($"PageSize cannot be greater than {MaxPageSize}.");
        }

        var (customers, totalCount) = await _customers.GetPagedAsync(
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / query.PageSize);

        var items = customers
            .Select(customer => new GetCustomersItemResult(
                customer.Id,
                customer.Name,
                customer.Email,
                customer.Phone,
                customer.Status.ToString()))
            .ToList();

        return new GetCustomersResult(items, query.PageNumber, query.PageSize, totalCount, totalPages);
    }
}
