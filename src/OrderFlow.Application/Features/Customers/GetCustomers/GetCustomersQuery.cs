using OrderFlow.Application.Abstractions.Messaging;

namespace OrderFlow.Application.Features.Customers.GetCustomers;

public sealed record GetCustomersQuery(int PageNumber = 1, int PageSize = 20, string? Search = null)
    : IQuery<GetCustomersResult>;
